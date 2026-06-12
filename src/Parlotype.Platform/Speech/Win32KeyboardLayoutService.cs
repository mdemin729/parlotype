using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Windows keyboard-layout detection via Win32. Reads the layout of the
/// foreground window's thread — keyboard layouts are per-thread on Windows, and
/// the foreground app's layout is the one the user is actually typing with
/// (Parlotype itself is a background tray app whose own thread layout may lag).
/// </summary>
public sealed class Win32KeyboardLayoutService : IKeyboardLayoutService
{
    private readonly ILogger<Win32KeyboardLayoutService> _logger;

    public Win32KeyboardLayoutService(ILogger<Win32KeyboardLayoutService> logger)
    {
        _logger = logger;
    }

    public KeyboardLayoutInfo? Detect()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var foreground = GetForegroundWindow();
            var threadId = foreground != 0
                ? GetWindowThreadProcessId(foreground, out _)
                : 0u; // 0 = current thread, the best remaining guess
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

            _logger.LogDebug("Keyboard layout detected: {LangId:X4} → {Code} ({Name})",
                langId, code, culture.EnglishName);
            return new KeyboardLayoutInfo(code, culture.EnglishName);
        }
        catch (CultureNotFoundException)
        {
            // Transient or custom LANGIDs (e.g. the 0x2000 LOCALE_TRANSIENT range)
            // have no culture data; treat them as "detection unavailable".
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint idThread);
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
