using System.Globalization;
using System.Runtime.InteropServices;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Windows keyboard-layout detection via Win32. Keyboard layouts are per-thread
/// on Windows, and the layout we want is the one the user is actually typing
/// with — i.e. the layout of the thread that owns the focused input control of
/// the foreground application.
/// <para>
/// Naively reading <c>GetWindowThreadProcessId(GetForegroundWindow())</c> is not
/// enough for modern packaged / XAML-island apps (e.g. the Windows 11 Notepad),
/// which spread their UI across many threads: the top-level frame window runs on
/// one thread while the text-input control runs on another. The frame thread's
/// layout is stale (it never processes the Alt+Shift language change), so we use
/// <see cref="GetGUIThreadInfo"/> to drill from the foreground thread into the
/// window that actually holds keyboard focus and read <em>its</em> thread. For
/// classic single-threaded apps the focus window lives on the same thread, so
/// this is identical to the foreground-thread read.
/// </para>
/// </summary>
public sealed class Win32KeyboardLayoutService : IKeyboardLayoutService
{
    public KeyboardLayoutInfo? Detect()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var threadId = ResolveInputThread();
            var hkl = GetKeyboardLayout(threadId);
            if (hkl == 0)
                return null;

            // The low word of the HKL is the input-language LANGID; the high word
            // is the device handle (0xF... for custom layouts) and is irrelevant here.
            var langId = (int)((nuint)hkl & 0xFFFF);
            var culture = CultureInfo.GetCultureInfo(langId);

            var code = culture.TwoLetterISOLanguageName;
            if (string.IsNullOrWhiteSpace(code) || code == "iv")
                return null;

            return new KeyboardLayoutInfo(code, culture.EnglishName);
        }
        catch (CultureNotFoundException)
        {
            // Transient or custom LANGIDs (e.g. the 0x2000 LOCALE_TRANSIENT range)
            // have no culture data; treat them as "detection unavailable".
            return null;
        }
    }

    /// <summary>
    /// Resolves the thread whose keyboard layout reflects what the user is typing:
    /// the thread owning the focused control of the foreground window, falling back
    /// to the foreground window's own thread, and finally to the current thread.
    /// </summary>
    private static uint ResolveInputThread()
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0)
            return 0u; // 0 = current thread, the best remaining guess

        var foregroundThread = GetWindowThreadProcessId(foreground, out _);

        // Cross from the (possibly stale) frame thread into the window that
        // actually holds keyboard focus. GetGUIThreadInfo reports the focus window
        // of the foreground input queue even when it is owned by another thread.
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(foregroundThread, ref info) && info.hwndFocus != 0)
        {
            var focusThread = GetWindowThreadProcessId(info.hwndFocus, out _);
            if (focusThread != 0)
                return focusThread;
        }

        return foregroundThread;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public int rcCaretLeft;
        public int rcCaretTop;
        public int rcCaretRight;
        public int rcCaretBottom;
    }
}

/// <summary>
/// Non-Windows fallback: keyboard-layout detection is unavailable, so the
/// <see cref="LanguageCatalog.KeyboardLayoutCode"/> source resolves to
/// auto-detect (see <see cref="SourceLanguageResolver"/>).
/// </summary>
public sealed class NoOpKeyboardLayoutService : IKeyboardLayoutService
{
    public KeyboardLayoutInfo? Detect() => null;
}
