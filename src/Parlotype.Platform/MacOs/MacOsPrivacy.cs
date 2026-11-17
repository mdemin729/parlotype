using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.MacOs;

/// <summary>
/// The two macOS privacy grants Parlotype depends on (ADR-077): Accessibility,
/// without which the global hotkey hook cannot be installed and Cmd+V cannot be
/// posted, and Microphone.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacOsPrivacy
{
    private const string ApplicationServicesLib =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    /// <summary>The value of <c>AVMediaTypeAudio</c>, which is the four-character code "soun".</summary>
    private const string AudioMediaType = "soun";

    // AVAuthorizationStatus
    private const long AuthorizationNotDetermined = 0;
    private const long AuthorizationAuthorized = 3;

    [DllImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(ApplicationServicesLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrustedWithOptions(nint options);

    // ---- Accessibility ----------------------------------------------------

    /// <summary>Whether this process may install event taps and post key events.</summary>
    internal static bool IsAccessibilityGranted => AXIsProcessTrusted();

    /// <summary>
    /// Asks macOS to show its own "would like to control this computer" dialog,
    /// which also adds the app to the Accessibility list so the user only has
    /// to flip the switch. macOS shows it at most once per app identity; later
    /// calls are silent.
    /// </summary>
    internal static void PromptForAccessibility()
    {
        // The exported constant kAXTrustedCheckOptionPrompt is a CFString with
        // exactly this value.
        var key = MacOsNative.CreateString("AXTrustedCheckOptionPrompt");
        var options = MacOsNative.CreateDictionary((key, MacOsNative.BooleanTrue));
        try
        {
            AXIsProcessTrustedWithOptions(options);
        }
        finally
        {
            MacOsNative.CFRelease(options);
            MacOsNative.CFRelease(key);
        }
    }

    // ---- Microphone -------------------------------------------------------

    /// <summary>What macOS currently says about this app and the microphone.</summary>
    internal static SystemPermissionState MicrophoneState
    {
        get
        {
            using var pool = MacOsNative.AutoreleasePool.Create();
            var device = CaptureDeviceClass();
            if (device == 0)
                return SystemPermissionState.Granted; // No AVFoundation: nothing gates capture.

            var mediaType = MacOsNative.CreateString(AudioMediaType);
            try
            {
                var status = (long)MacOsNative.Send(
                    device, MacOsNative.Selector("authorizationStatusForMediaType:"), mediaType);

                return status switch
                {
                    AuthorizationAuthorized => SystemPermissionState.Granted,
                    AuthorizationNotDetermined => SystemPermissionState.NotDetermined,
                    _ => SystemPermissionState.Denied, // restricted or denied
                };
            }
            finally
            {
                MacOsNative.CFRelease(mediaType);
            }
        }
    }

    /// <summary>
    /// Shows the system microphone prompt if the user has not answered it yet
    /// and completes with their answer. Completes immediately when the question
    /// was already settled.
    /// </summary>
    internal static Task<bool> RequestMicrophoneAsync()
    {
        var state = MicrophoneState;
        if (state != SystemPermissionState.NotDetermined)
            return Task.FromResult(state == SystemPermissionState.Granted);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (PendingRequests)
            PendingRequests.Add(completion);

        using var pool = MacOsNative.AutoreleasePool.Create();
        var mediaType = MacOsNative.CreateString(AudioMediaType);
        try
        {
            MacOsNative.Send(
                CaptureDeviceClass(),
                MacOsNative.Selector("requestAccessForMediaType:completionHandler:"),
                mediaType,
                CompletionBlock.Value);
        }
        finally
        {
            MacOsNative.CFRelease(mediaType);
        }

        return completion.Task;
    }

    private static nint CaptureDeviceClass()
    {
        MacOsNative.EnsureFrameworkLoaded("AVFoundation");
        return MacOsNative.Class("AVCaptureDevice");
    }

    // The completion handler is an Objective-C block. A block that captures
    // nothing can be a "global" block: a constant structure whose invoke
    // pointer the runtime calls with the block itself as the first argument.
    // That is all this needs, so the block is built once by hand rather than
    // pulling in a block-marshalling dependency.

    private static readonly List<TaskCompletionSource<bool>> PendingRequests = [];

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AccessHandler(nint block, [MarshalAs(UnmanagedType.I1)] bool granted);

    /// <summary>Held for the life of the process: the block below points at its thunk.</summary>
    private static readonly AccessHandler OnAccessAnswered = (_, granted) =>
    {
        TaskCompletionSource<bool>[] waiting;
        lock (PendingRequests)
        {
            waiting = [.. PendingRequests];
            PendingRequests.Clear();
        }

        foreach (var request in waiting)
            request.TrySetResult(granted);
    };

    private static readonly Lazy<nint> CompletionBlock = new(() =>
    {
        const int blockIsGlobal = 1 << 28;

        // struct Block_descriptor { unsigned long reserved; unsigned long size; }
        var descriptor = Marshal.AllocHGlobal(2 * nint.Size);
        Marshal.WriteIntPtr(descriptor, 0, 0);
        Marshal.WriteIntPtr(descriptor, nint.Size, 4 * nint.Size);

        // struct Block_literal { void *isa; int flags; int reserved; void *invoke; void *descriptor; }
        var block = Marshal.AllocHGlobal(4 * nint.Size);
        Marshal.WriteIntPtr(block, 0, NativeLibrary.GetExport(MacOsNative.SystemLibraryHandle, "_NSConcreteGlobalBlock"));
        Marshal.WriteInt32(block, nint.Size, blockIsGlobal);
        Marshal.WriteInt32(block, nint.Size + 4, 0);
        Marshal.WriteIntPtr(block, 2 * nint.Size, Marshal.GetFunctionPointerForDelegate(OnAccessAnswered));
        Marshal.WriteIntPtr(block, 3 * nint.Size, descriptor);
        return block;
    });

    /// <summary>
    /// Test seam: has the system invoke the hand-built completion block, the
    /// way AVFoundation does after the user answers, without showing a prompt.
    /// libdispatch copies the block and calls it on a worker thread, which is
    /// what proves its layout is one the Objective-C runtime accepts. The
    /// answer it delivers is meaningless (libdispatch passes no argument), so
    /// only completion is asserted. Never used by the app.
    /// </summary>
    internal static Task InvokeCompletionBlockForTestAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (PendingRequests)
            PendingRequests.Add(completion);

        DispatchAsync(DispatchGetGlobalQueue(0, 0), CompletionBlock.Value);
        return completion.Task;
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dispatch_get_global_queue")]
    private static extern nint DispatchGetGlobalQueue(nint identifier, nuint flags);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dispatch_async")]
    private static extern void DispatchAsync(nint queue, nint block);

    // ---- System Settings --------------------------------------------------

    /// <summary>Opens System Settings on the Privacy &amp; Security pane for a permission.</summary>
    internal static bool OpenSettings(SystemPermission permission)
    {
        var anchor = permission == SystemPermission.Microphone ? "Privacy_Microphone" : "Privacy_Accessibility";

        try
        {
            using var process = Process.Start(new ProcessStartInfo("/usr/bin/open")
            {
                ArgumentList = { $"x-apple.systempreferences:com.apple.preference.security?{anchor}" },
                UseShellExecute = false,
            });
            return process is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
