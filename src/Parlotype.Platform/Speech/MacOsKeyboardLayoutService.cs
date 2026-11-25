using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Parlotype.Core.Speech;
using Parlotype.Platform.MacOs;

namespace Parlotype.Platform.Speech;

/// <summary>
/// macOS keyboard-layout detection through Text Input Sources (ADR-078): the
/// language of the input source the user is typing with right now.
/// </summary>
/// <remarks>
/// <para>
/// Text Input Sources may only be called on the main thread. Recent macOS
/// versions assert that and abort the process when it is violated, so a call
/// from any other thread is queued on the main dispatch queue and awaited.
/// </para>
/// <para>
/// That queue is drained by AppKit's run loop, which exists in the desktop app
/// and not in a test host or the benchmark CLI. The wait therefore has a short
/// timeout and answers "detection unavailable" when it expires, which callers
/// already treat as auto-detect.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacOsKeyboardLayoutService : IKeyboardLayoutService
{
    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";

    private static readonly TimeSpan MainThreadTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Lazy<nint> CarbonHandle = new(() => NativeLibrary.Load(CarbonLib));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DispatchWork(nint context);

    /// <summary>Held for the life of the process: libdispatch calls its thunk.</summary>
    private static readonly DispatchWork RunPendingOnMainThread = _ => CompletePending();

    private static readonly Lock PendingGate = new();
    private static readonly List<TaskCompletionSource<string?>> Pending = [];

    public KeyboardLayoutInfo? Detect()
    {
        try
        {
            var code = MacOsNative.IsMainThread ? ReadCurrentLanguage() : ReadCurrentLanguageOnMainThread();
            if (string.IsNullOrWhiteSpace(code))
                return null;

            // Input sources report BCP 47 tags ("en", "pt-BR", "zh-Hans").
            var culture = CultureInfo.GetCultureInfo(code);
            var language = culture.TwoLetterISOLanguageName;
            if (string.IsNullOrWhiteSpace(language) || language == "iv")
                return null;

            return new KeyboardLayoutInfo(language, culture.EnglishName);
        }
        catch (Exception)
        {
            // CultureNotFoundException for an exotic tag, or a framework that
            // would not load: either way, detection is unavailable.
            return null;
        }
    }

    private static string? ReadCurrentLanguageOnMainThread()
    {
        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (PendingGate)
            Pending.Add(request);

        MacOsNative.DispatchToMainQueue(Marshal.GetFunctionPointerForDelegate(RunPendingOnMainThread));

        if (request.Task.Wait(MainThreadTimeout))
            return request.Task.Result;

        // Nothing is draining the main queue (or it is busy). Withdraw the
        // request so a late run does not complete a task nobody reads.
        lock (PendingGate)
            Pending.Remove(request);

        return null;
    }

    private static void CompletePending()
    {
        TaskCompletionSource<string?>[] waiting;
        lock (PendingGate)
        {
            waiting = [.. Pending];
            Pending.Clear();
        }

        if (waiting.Length == 0)
            return;

        string? language = null;
        try
        {
            language = ReadCurrentLanguage();
        }
        catch (Exception)
        {
            // Must not unwind into libdispatch; null already means "unavailable".
        }

        foreach (var request in waiting)
            request.TrySetResult(language);
    }

    /// <summary>Main thread only.</summary>
    private static string? ReadCurrentLanguage()
    {
        var source = TISCopyCurrentKeyboardInputSource();
        if (source == 0)
            return null;

        try
        {
            // Not retained by the Get rule: valid for as long as the source is.
            var languages = TISGetInputSourceProperty(
                source, MacOsNative.ReadPointerConstant(CarbonHandle.Value, "kTISPropertyInputSourceLanguages"));

            if (languages == 0 || MacOsNative.CFArrayGetCount(languages) == 0)
                return null;

            // The first entry is the language the source is primarily for.
            return MacOsNative.ReadString(MacOsNative.CFArrayGetValueAtIndex(languages, 0));
        }
        finally
        {
            MacOsNative.CFRelease(source);
        }
    }

    [DllImport(CarbonLib)]
    private static extern nint TISCopyCurrentKeyboardInputSource();

    [DllImport(CarbonLib)]
    private static extern nint TISGetInputSourceProperty(nint inputSource, nint propertyKey);
}
