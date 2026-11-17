using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Parlotype.Platform.MacOs;

namespace Parlotype.Platform.Audio;

/// <summary>
/// P/Invoke surface for macOS audio: the Audio Queue Services used to record,
/// and the CoreAudio hardware-abstraction layer used to list input devices
/// (ADR-077).
/// </summary>
[SupportedOSPlatform("macos")]
internal static class CoreAudioInterop
{
    private const string AudioToolboxLib = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";

    internal const int NoError = 0;

    // ---- Audio Queue Services --------------------------------------------

    /// <summary><c>kAudioFormatLinearPCM</c>.</summary>
    internal const uint FormatLinearPcm = 0x6C70636D; // 'lpcm'

    /// <summary><c>kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked</c>, native byte order.</summary>
    internal const uint FormatFlagsFloatPacked = 0x1 | 0x8;

    /// <summary><c>kAudioQueueProperty_CurrentDevice</c>: a <c>CFStringRef</c> holding a device UID.</summary>
    internal const uint QueuePropertyCurrentDevice = 0x61716364; // 'aqcd'

    /// <summary>Matches the native <c>AudioStreamBasicDescription</c> (40 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    /// <summary>
    /// Offsets into the native <c>AudioQueueBuffer</c> on a 64-bit process:
    /// a 32-bit capacity, 4 bytes of padding, the data pointer, then the size
    /// actually filled.
    /// </summary>
    internal const int BufferDataPointerOffset = 8;
    internal const int BufferDataByteSizeOffset = 16;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void AudioQueueInputCallback(
        nint userData, nint queue, nint buffer, nint startTime, uint packetDescriptionCount, nint packetDescriptions);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueNewInput(
        ref AudioStreamBasicDescription format,
        AudioQueueInputCallback callback,
        nint userData,
        nint callbackRunLoop,
        nint callbackRunLoopMode,
        uint flags,
        out nint queue);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueAllocateBuffer(nint queue, uint bufferByteSize, out nint buffer);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueEnqueueBuffer(nint queue, nint buffer, uint packetDescriptionCount, nint packetDescriptions);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueStart(nint queue, nint startTime);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueStop(nint queue, [MarshalAs(UnmanagedType.I1)] bool immediate);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueDispose(nint queue, [MarshalAs(UnmanagedType.I1)] bool immediate);

    [DllImport(AudioToolboxLib)]
    internal static extern int AudioQueueSetProperty(nint queue, uint propertyId, ref nint data, uint dataSize);

    // ---- CoreAudio hardware-abstraction layer ----------------------------

    /// <summary><c>kAudioObjectSystemObject</c>.</summary>
    internal const uint SystemObject = 1;

    internal const uint PropertyDevices = 0x64657623;            // 'dev#' kAudioHardwarePropertyDevices
    internal const uint PropertyDefaultInputDevice = 0x64496E20; // 'dIn ' kAudioHardwarePropertyDefaultInputDevice
    internal const uint PropertyStreams = 0x73746D23;            // 'stm#' kAudioDevicePropertyStreams
    internal const uint PropertyDeviceUid = 0x75696420;          // 'uid ' kAudioDevicePropertyDeviceUID
    internal const uint PropertyName = 0x6C6E616D;               // 'lnam' kAudioObjectPropertyName

    internal const uint ScopeGlobal = 0x676C6F62; // 'glob'
    internal const uint ScopeInput = 0x696E7074;  // 'inpt'
    internal const uint ElementMain = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyAddress
    {
        public uint Selector;
        public uint Scope;
        public uint Element;

        public PropertyAddress(uint selector, uint scope)
        {
            Selector = selector;
            Scope = scope;
            Element = ElementMain;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int PropertyListener(uint objectId, uint addressCount, nint addresses, nint clientData);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyDataSize(
        uint objectId, ref PropertyAddress address, uint qualifierSize, nint qualifier, out uint dataSize);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyData(
        uint objectId, ref PropertyAddress address, uint qualifierSize, nint qualifier, ref uint dataSize, [Out] uint[] data);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyData(
        uint objectId, ref PropertyAddress address, uint qualifierSize, nint qualifier, ref uint dataSize, out uint data);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectGetPropertyData(
        uint objectId, ref PropertyAddress address, uint qualifierSize, nint qualifier, ref uint dataSize, out nint data);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectAddPropertyListener(
        uint objectId, ref PropertyAddress address, PropertyListener listener, nint clientData);

    [DllImport(CoreAudioLib)]
    internal static extern int AudioObjectRemovePropertyListener(
        uint objectId, ref PropertyAddress address, PropertyListener listener, nint clientData);

    /// <summary>Reads a property whose value is a <c>CFStringRef</c> the caller owns.</summary>
    internal static string? ReadStringProperty(uint objectId, uint selector)
    {
        var address = new PropertyAddress(selector, ScopeGlobal);
        var size = (uint)nint.Size;

        if (AudioObjectGetPropertyData(objectId, ref address, 0, 0, ref size, out nint cfString) != NoError || cfString == 0)
            return null;

        try
        {
            return MacOsNative.ReadString(cfString);
        }
        finally
        {
            MacOsNative.CFRelease(cfString);
        }
    }

    /// <summary>
    /// Formats an <c>OSStatus</c> the way Apple's headers spell it: most audio
    /// errors are four printable characters, and "'!dev'" is far easier to look
    /// up than 560227702.
    /// </summary>
    internal static string DescribeStatus(int status)
    {
        Span<char> chars =
        [
            (char)((status >> 24) & 0xFF),
            (char)((status >> 16) & 0xFF),
            (char)((status >> 8) & 0xFF),
            (char)(status & 0xFF),
        ];

        foreach (var c in chars)
        {
            if (c is < ' ' or > '~')
                return status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return $"'{new string(chars)}' ({status})";
    }
}
