using System.Globalization;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Localization;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// What the app says on a Mac (ADR-077): Apple key names, and the macOS wording
/// of the copy that names Windows things. Read through the overloads that take
/// the flavour as an argument, so nothing process-wide changes under the tests
/// running beside these (see <see cref="TestPlatformDefaults"/>).
/// </summary>
[Collection(CultureBoundTests.Name)]
public class MacPresentationTests
{
    // --- Key names ---

    [Fact]
    public void Gestures_UseAppleKeyNames()
    {
        using var _ = CultureScope.Use("en");

        Assert.Equal("Hold Right Cmd", HotkeyText.Gesture(DictationHotkeyDefaults.MacPushToTalk.Gesture, macKeyNames: true));
        Assert.Equal("Hold Right Option",
            HotkeyText.Gesture(HotkeyGesture.ForHold(ModifierKey.Alt, ModifierSide.Right), macKeyNames: true));
        Assert.Equal("Double-tap Ctrl", HotkeyText.Gesture(DictationHotkeyDefaults.Toggle.Gesture, macKeyNames: true));
    }

    [Fact]
    public void Chords_UseAppleKeyNames_InTheInvariantOrder()
    {
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(
                HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Meta, "Space"),
            ActivationMode.Toggle);

        Assert.Equal("Ctrl+Option+Shift+Cmd+Space", HotkeyText.Gesture(chord.Gesture, macKeyNames: true));
        // The same chord on a PC keyboard, and in the logs on every machine.
        Assert.Equal("Ctrl+Alt+Shift+Win+Space", HotkeyText.Gesture(chord.Gesture, macKeyNames: false));
        Assert.Equal("Ctrl+Alt+Shift+Win+Space", chord.Gesture.DisplayString);
    }

    [Fact]
    public void TheAltGrWarning_IsNotShownOnAMac()
    {
        using var _ = CultureScope.Use("en");

        var candidate = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "E"), ActivationMode.Toggle);
        var conflict = HotkeyConflictDetector.Check(candidate);

        Assert.Equal(HotkeyConflictReason.AltGrCollision, conflict.Reason);
        Assert.NotNull(HotkeyText.Conflict(candidate, conflict, macKeyNames: false));
        Assert.Null(HotkeyText.Conflict(candidate, conflict, macKeyNames: true));
    }

    // --- Wording variants ---

    [Fact]
    public void AKeyWithAMacVariant_ReadsDifferentlyOnAMac()
    {
        using var _ = CultureScope.Use("en");

        var windows = Localizer.Lookup(nameof(Strings.Onboarding_Tray_Title), macVariants: false, windowsFileNames: true);
        var mac = Localizer.Lookup(nameof(Strings.Onboarding_Tray_Title), macVariants: true, windowsFileNames: false);

        Assert.Equal("Parlotype lives in the tray", windows);
        Assert.Equal("Parlotype lives in the menu bar", mac);
    }

    [Fact]
    public void AKeyWithoutAMacVariant_ReadsTheSameEverywhere()
    {
        using var _ = CultureScope.Use("en");

        Assert.Equal(
            Localizer.Lookup(nameof(Strings.Common_Cancel), macVariants: false, windowsFileNames: true),
            Localizer.Lookup(nameof(Strings.Common_Cancel), macVariants: true, windowsFileNames: false));
    }

    [Fact]
    public void AskingForAVariantKeyDirectly_DoesNotLookForAVariantOfTheVariant()
    {
        using var _ = CultureScope.Use("en");

        Assert.Equal(
            "Parlotype lives in the menu bar",
            Localizer.Lookup(nameof(Strings.Onboarding_Tray_Title_Mac), macVariants: true, windowsFileNames: false));
    }

    /// <summary>
    /// Every variant must have a base key to stand in for. An orphan would be
    /// translated into every language and never shown.
    /// </summary>
    [Fact]
    public void EveryMacVariant_HasABaseKey()
    {
        var keys = typeof(Strings).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var variants = keys.Where(k => k.EndsWith(Localizer.MacVariantSuffix, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(variants);
        Assert.All(variants, v => Assert.Contains(v[..^Localizer.MacVariantSuffix.Length], keys));
    }

    /// <summary>
    /// A variant exists to stop the Mac copy naming Windows things. Checked in
    /// every shipped language, because a translator starting from the Windows
    /// string is exactly how "Windows" would survive into the macOS one.
    /// </summary>
    [Fact]
    public void NoMacVariant_MentionsWindowsOrItsTools_InAnyLanguage()
    {
        var variants = typeof(Strings).GetProperties()
            .Select(p => p.Name)
            .Where(k => k.EndsWith(Localizer.MacVariantSuffix, StringComparison.Ordinal))
            .ToList();

        string[] windowsOnly = ["Windows", "Setup.exe", "HKCU", "Task Manager"];

        foreach (var language in SupportedUiLanguages.All)
        {
            using var _ = CultureScope.Use(language.CultureName);

            foreach (var key in variants)
            {
                var text = Localizer.Lookup(key, macVariants: true, windowsFileNames: false);
                Assert.False(
                    windowsOnly.Any(term => text.Contains(term, StringComparison.Ordinal)),
                    $"{key} [{language.CultureName}] still names a Windows thing: {text}");
            }
        }
    }

    // --- File names ---

    [Fact]
    public void TheServerBinary_LosesItsExeExtensionOffWindows_InEveryLanguage()
    {
        string[] keys =
        [
            nameof(Strings.Settings_LlamaCpp_ManualHint),
            nameof(Strings.Settings_LlamaCpp_ManualFolderPlaceholder),
            nameof(Strings.Settings_LlamaCpp_ManualFolderRequiredError),
        ];

        foreach (var language in SupportedUiLanguages.All)
        {
            using var _ = CultureScope.Use(language.CultureName);

            foreach (var key in keys)
            {
                // File names are never translated, so the token is there to rewrite.
                Assert.Contains("llama-server.exe", Localizer.Lookup(key, macVariants: false, windowsFileNames: true));

                var unix = Localizer.Lookup(key, macVariants: true, windowsFileNames: false);
                Assert.Contains("llama-server", unix);
                Assert.DoesNotContain(".exe", unix);
            }
        }
    }

    /// <summary>Sets the interface culture for one test and puts the previous one back.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        private CultureScope(string code) =>
            CultureInfo.CurrentUICulture = code == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(code);

        public static CultureScope Use(string code) => new(code);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }
}
