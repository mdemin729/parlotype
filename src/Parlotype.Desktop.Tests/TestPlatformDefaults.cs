using System.Runtime.CompilerServices;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.ViewModels.Settings;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Pins everything the app derives from the operating system — wording, key
/// names, default hotkeys, default engine — to the Windows flavour for the
/// whole test assembly (ADR-078).
/// </summary>
/// <remarks>
/// <para>
/// On a Mac the app says "menu bar", "Option" and "Cmd" where it says "tray",
/// "Alt" and "Win" on Windows. The existing tests assert the Windows text, and
/// they have to mean the same thing on a developer's Mac as on Windows CI, so
/// the choice is fixed here, once, before any test runs. The same goes for
/// the two defaults Core derives from the OS: the hotkey set (no Right Ctrl on
/// a MacBook) and the engine (no Parakeet on macOS older than 14.4).
/// </para>
/// <para>
/// It is never flipped back and forth: test classes run in parallel, and a
/// test that switched a process-wide flag would change the text under every
/// other test. The macOS flavour is tested through the overloads that take the
/// flavour as an argument (<c>MacPresentationTests</c>).
/// </para>
/// </remarks>
internal static class TestPlatformDefaults
{
#pragma warning disable CA2255 // A test assembly is the one place a module initializer is the right tool.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void PinWindowsPresentation()
    {
        Localizer.UseMacVariants = false;
        Localizer.UseWindowsFileNames = true;
        HotkeyText.UseMacKeyNames = false;
        DictationHotkeyDefaults.PinnedSet = DictationHotkeyDefaults.All;
        SpeechEngineDefaults.PinnedDefault = SpeechEngine.Parakeet;
    }
}
