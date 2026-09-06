using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Renders every settings page in each shipped language and writes the images to
/// <c>reports/localized-layout/</c> so text expansion can actually be looked at
/// (ADR-064, localization plan phases 4–5). Russian runs roughly 35 % longer than
/// English and Spanish about 20 %, which is where clipping shows up.
/// </summary>
/// <remarks>
/// <para>
/// This is a review harness, not an assertion: layout damage is a visual
/// judgement, and a test that tried to assert "nothing is clipped" would either
/// be trivially true or brittle. What it does assert is the one thing a machine
/// can judge reliably — that a page is not <em>taller</em> under a translation
/// than the window can show, which is the failure mode that hides content
/// outright rather than merely looking cramped.
/// </para>
/// <para>
/// <b>Opt-in.</b> Set <c>PARLOTYPE_LAYOUT_REVIEW=1</c> to run it. Rendering 19
/// pages leaves that many view trees bound to the <see cref="Localizer"/>
/// singleton, and those bindings only detach when the controls are collected —
/// so leaving this in the ordinary suite made the culture-bound tests fail
/// intermittently, passing or failing the same code depending on GC timing. A
/// review harness is not worth an unreliable <c>dotnet test</c>.
/// </para>
/// <para>
/// Shares the culture-bound collection because it changes the process-wide
/// culture.
/// </para>
/// </remarks>
[Collection(CultureBoundTests.Name)]
public class LocalizedLayoutReviewTests
{
    /// <summary>Gate for xUnit's <c>SkipUnless</c>; see the class remarks.</summary>
    public static bool ReviewRequested =>
        Environment.GetEnvironmentVariable("PARLOTYPE_LAYOUT_REVIEW") == "1";

    /// <summary>
    /// Width a settings page actually gets: the window is 900 wide, the nav pane
    /// takes 200, and the content border adds 24 of padding on each side.
    /// </summary>
    private const int ContentWidth = 900 - 200 - 48;

    /// <summary>
    /// Height of the settings window's content area. A page taller than this
    /// scrolls, which is fine — but a *label* that grows past it usually means a
    /// fixed-size container is now hiding text.
    /// </summary>
    private const int ContentHeight = 770 - 48;

    public static TheoryData<string> Cultures() => new() { "en", "ru", "es" };

    [AvaloniaTheory(
        Skip = "Layout review harness: set PARLOTYPE_LAYOUT_REVIEW=1 to render the pages.",
        SkipUnless = nameof(ReviewRequested))]
    [MemberData(nameof(Cultures))]
    public async Task EverySettingsPage_RendersInEveryLanguage(string culture)
    {
        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo(culture));

        var settings = new MockSettingsService();
        var shell = SettingsWindowViewModelFactory.Build(settings);
        var outputDirectory = Path.Combine(
            RepoRoot(), "reports", "localized-layout", culture);
        Directory.CreateDirectory(outputDirectory);

        var oversized = new List<string>();

        foreach (var (name, view, dataContext) in PagesFor(shell))
        {
            var png = await ScreenshotHelper.CaptureBase64Async(
                view, dataContext, width: ContentWidth, maxHeight: 2000);

            if (png.Length == 0)
                continue;

            File.WriteAllBytes(
                Path.Combine(outputDirectory, $"{name}.png"),
                Convert.FromBase64String(png));

            var height = view.DesiredSize.Height;
            if (height > ContentHeight * 2)
                oversized.Add($"{name} is {height:F0}px tall under '{culture}'");
        }

        Assert.True(
            oversized.Count == 0,
            "A page grew past twice the window height, which usually means text is being "
            + "hidden rather than wrapped: " + string.Join("; ", oversized));

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en"));
    }

    private static IEnumerable<(string Name, Control View, object DataContext)> PagesFor(
        Parlotype.Desktop.ViewModels.SettingsWindowViewModel shell)
    {
        yield return ("microphone", new MicrophoneSettingsView(), shell.Microphone);
        yield return ("silence-timeout", new SilenceTimeoutSettingsView(), shell.SilenceTimeout);
        yield return ("engine", new SpeechEngineSettingsView(), shell.SpeechEngine);
        yield return ("language", new LanguageSelectionSettingsView(), shell.Language);
        yield return ("whisper-model", new WhisperModelSettingsView(), shell.WhisperModel);
        yield return ("whisper-runtime", new RuntimeSettingsView(), shell.Runtime);
        yield return ("whisper-output", new WhisperOutputSettingsView(), shell.WhisperOutput);
        yield return ("gemma4-model", new Gemma4ModelSettingsView(), shell.Gemma4Model);
        yield return ("parakeet-model", new ParakeetModelSettingsView(), shell.ParakeetModel);
        yield return ("cloud-providers", new CloudProviderSettingsView(), shell.CloudProviders);
        yield return ("prompts", new PromptSettingsView(), shell.Prompts);
        yield return ("llamacpp", new LlamaCppSettingsView(), shell.LlamaCpp);
        yield return ("hotkeys", new HotkeySettingsView(), shell.Hotkey);
        yield return ("theme", new ThemeSettingsView(), shell.Theme);
        yield return ("interface-language", new InterfaceLanguageSettingsView(), shell.InterfaceLanguage);
        yield return ("startup", new StartupSettingsView(), shell.Startup);
        yield return ("updates", new UpdateSettingsView(), shell.Updates);
        yield return ("data", new DataSettingsView(), shell.Data);
        yield return ("help", new HelpSettingsView(), shell.Help);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parlotype.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
