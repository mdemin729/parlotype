using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Parlotype.Desktop.Views;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The warn (translation unavailable / paused) palette is declared per view rather
/// than once in <c>App.axaml</c> — the headless renderer hosts controls under
/// <see cref="TestApp"/>, so an application-scoped dictionary resolves to nothing
/// here and every amber state renders as plain text with no visible failure.
/// These tests pin the palette to the surfaces that use it (ADR-061).
/// </summary>
public class WarnPaletteResourceTests
{
    public static TheoryData<string> WarnKeys => new() { "WarnForegroundBrush", "WarnBackgroundBrush" };

    private static void AssertResolves(IResourceHost host, string key)
    {
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Assert.True(
                host.TryFindResource(key, variant, out var value),
                $"{host.GetType().Name} does not expose {key} for {variant}.");
            Assert.IsAssignableFrom<IBrush>(value);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(WarnKeys))]
    public void LanguagePage_ExposesTheWarnPalette(string key) =>
        AssertResolves(new LanguageSelectionSettingsView(), key);

    [AvaloniaTheory]
    [MemberData(nameof(WarnKeys))]
    public void WhisperModelPage_ExposesTheWarnPalette(string key) =>
        AssertResolves(new WhisperModelSettingsView(), key);

    [AvaloniaTheory]
    [MemberData(nameof(WarnKeys))]
    public void TranscribeWindow_ExposesTheWarnPalette(string key) =>
        AssertResolves(new TranscribeWindow(), key);
}
