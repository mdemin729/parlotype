using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Parlotype.Platform.MacOs;

/// <summary>
/// The CoreFoundation and Objective-C runtime calls every macOS service in
/// Platform shares (ADR-077).
/// </summary>
/// <remarks>
/// <para>
/// Parlotype talks to macOS through plain P/Invoke rather than a binding
/// package: the surface it needs is small (a pasteboard, a running-application
/// lookup, the keychain, an audio queue), and a dependency for it would be one
/// more native payload to sign and notarize.
/// </para>
/// <para>
/// Two rules keep that honest. <b>No struct returns</b> through
/// <c>objc_msgSend</c>: on x64 those need <c>objc_msgSend_stret</c> and on arm64
/// they do not, so every message sent here returns a pointer, an integer or
/// nothing. And <b>every call made off the main thread sits inside an
/// autorelease pool</b> (<see cref="AutoreleasePool"/>), because a thread the
/// runtime did not create has none and would leak each autoreleased object.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
internal static class MacOsNative
{
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string SystemLib = "/usr/lib/libSystem.B.dylib";

    private const uint Utf8Encoding = 0x08000100; // kCFStringEncodingUTF8

    private static readonly Lazy<nint> CoreFoundationHandle = new(() => NativeLibrary.Load(CoreFoundationLib));
    private static readonly Lazy<nint> SystemHandle = new(() => NativeLibrary.Load(SystemLib));

    // ---- CoreFoundation ---------------------------------------------------

    [DllImport(CoreFoundationLib)]
    internal static extern void CFRelease(nint cf);

    [DllImport(CoreFoundationLib)]
    private static extern nint CFStringCreateWithBytes(
        nint allocator, byte[] bytes, nint numBytes, uint encoding, [MarshalAs(UnmanagedType.I1)] bool isExternalRepresentation);

    [DllImport(CoreFoundationLib)]
    private static extern nint CFStringGetLength(nint cfString);

    [DllImport(CoreFoundationLib)]
    private static extern nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [DllImport(CoreFoundationLib)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(nint cfString, byte[] buffer, nint bufferSize, uint encoding);

    [DllImport(CoreFoundationLib)]
    internal static extern nint CFArrayGetCount(nint cfArray);

    [DllImport(CoreFoundationLib)]
    internal static extern nint CFArrayGetValueAtIndex(nint cfArray, nint index);

    [DllImport(CoreFoundationLib)]
    internal static extern nint CFDataCreate(nint allocator, byte[] bytes, nint length);

    [DllImport(CoreFoundationLib)]
    internal static extern nint CFDataGetLength(nint cfData);

    [DllImport(CoreFoundationLib)]
    internal static extern nint CFDataGetBytePtr(nint cfData);

    [DllImport(CoreFoundationLib)]
    private static extern nint CFDictionaryCreate(
        nint allocator, nint[] keys, nint[] values, nint count, nint keyCallBacks, nint valueCallBacks);

    /// <summary>Creates a CFString (toll-free bridged to NSString). The caller releases it.</summary>
    internal static nint CreateString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return CFStringCreateWithBytes(0, bytes, bytes.Length, Utf8Encoding, isExternalRepresentation: false);
    }

    /// <summary>Copies a CFString or NSString into a managed string. Null in, null out.</summary>
    internal static string? ReadString(nint cfString)
    {
        if (cfString == 0)
            return null;

        var length = CFStringGetLength(cfString);
        if (length == 0)
            return string.Empty;

        var capacity = (int)CFStringGetMaximumSizeForEncoding(length, Utf8Encoding) + 1;
        var buffer = new byte[capacity];
        if (!CFStringGetCString(cfString, buffer, capacity, Utf8Encoding))
            return null;

        var terminator = Array.IndexOf(buffer, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, terminator < 0 ? buffer.Length : terminator);
    }

    /// <summary>
    /// Creates an immutable CFDictionary that retains its keys and values. The
    /// caller releases the dictionary; the keys and values stay owned by whoever
    /// created them.
    /// </summary>
    internal static nint CreateDictionary(params (nint Key, nint Value)[] entries)
    {
        var keys = new nint[entries.Length];
        var values = new nint[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            keys[i] = entries[i].Key;
            values[i] = entries[i].Value;
        }

        return CFDictionaryCreate(
            0, keys, values, entries.Length,
            NativeLibrary.GetExport(CoreFoundationHandle.Value, "kCFTypeDictionaryKeyCallBacks"),
            NativeLibrary.GetExport(CoreFoundationHandle.Value, "kCFTypeDictionaryValueCallBacks"));
    }

    /// <summary><c>kCFBooleanTrue</c>. A process-lifetime constant; never release it.</summary>
    internal static nint BooleanTrue =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(CoreFoundationHandle.Value, "kCFBooleanTrue"));

    /// <summary>
    /// Reads an exported constant that is itself a pointer, such as the
    /// <c>CFStringRef</c> keys the Security and Carbon frameworks export.
    /// </summary>
    internal static nint ReadPointerConstant(nint libraryHandle, string symbol) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(libraryHandle, symbol));

    // ---- Objective-C runtime ----------------------------------------------

    [DllImport(ObjCLib, EntryPoint = "objc_getClass")]
    private static extern nint ObjcGetClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(ObjCLib, EntryPoint = "sel_registerName")]
    private static extern nint SelRegisterName([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(ObjCLib, EntryPoint = "objc_autoreleasePoolPush")]
    private static extern nint AutoreleasePoolPush();

    [DllImport(ObjCLib, EntryPoint = "objc_autoreleasePoolPop")]
    private static extern void AutoreleasePoolPop(nint pool);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    internal static extern nint SendInt32(nint receiver, nint selector, int arg1);

    /// <summary>The class with this name, or 0 when its framework is not loaded.</summary>
    internal static nint Class(string name) => ObjcGetClass(name);

    internal static nint Selector(string name) => SelRegisterName(name);

    /// <summary>
    /// An Objective-C <c>BOOL</c> comes back in the low byte of the return
    /// register; the upper bits are not guaranteed to be clear.
    /// </summary>
    internal static bool AsBool(nint value) => (value & 0xFF) != 0;

    /// <summary>
    /// Loads a framework so its classes are registered with the runtime. In the
    /// desktop app AppKit is already loaded by Avalonia; in a test host or the
    /// benchmark CLI nothing has loaded it yet.
    /// </summary>
    internal static void EnsureFrameworkLoaded(string frameworkName) =>
        NativeLibrary.Load($"/System/Library/Frameworks/{frameworkName}.framework/{frameworkName}");

    /// <summary>
    /// A scope during which autoreleased Objective-C objects are collected.
    /// Required around any message send made from a thread AppKit does not own.
    /// </summary>
    internal readonly struct AutoreleasePool : IDisposable
    {
        private readonly nint _pool;

        private AutoreleasePool(nint pool) => _pool = pool;

        public static AutoreleasePool Create() => new(AutoreleasePoolPush());

        public void Dispose()
        {
            if (_pool != 0)
                AutoreleasePoolPop(_pool);
        }
    }

    // ---- libSystem --------------------------------------------------------

    [DllImport(SystemLib, EntryPoint = "pthread_main_np")]
    private static extern int PthreadMainNp();

    [DllImport(SystemLib, EntryPoint = "dispatch_async_f")]
    private static extern void DispatchAsyncF(nint queue, nint context, nint work);

    /// <summary>True when the calling thread is the process's main thread.</summary>
    internal static bool IsMainThread => PthreadMainNp() != 0;

    internal static nint SystemLibraryHandle => SystemHandle.Value;

    /// <summary>
    /// Queues <paramref name="work"/> (a <c>void (*)(void*)</c> function pointer)
    /// on the main dispatch queue. It runs only if something is draining that
    /// queue, which AppKit's run loop does in the desktop app and nothing does
    /// in a test host — callers must not wait on it without a timeout.
    /// </summary>
    internal static void DispatchToMainQueue(nint work, nint context = 0) =>
        // dispatch_get_main_queue() is a macro for the address of this symbol.
        DispatchAsyncF(NativeLibrary.GetExport(SystemHandle.Value, "_dispatch_main_q"), context, work);
}
