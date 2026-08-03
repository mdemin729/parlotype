using System.Globalization;
using System.Resources;

namespace Parlotype.Desktop.Resources;

/// <summary>
/// Strongly-typed accessor for <c>Strings.resx</c> — the app's externalized
/// user-facing copy (ADR-055). Hand-written rather than designer-generated so
/// the CLI build stays deterministic under warnings-as-errors. Translations are
/// added later as satellite <c>Strings.&lt;culture&gt;.resx</c> files; no
/// markup or code changes needed. A missing key falls back to the key name so
/// a stale resx never crashes the UI. Public so tests can verify every key
/// resolves (Desktop has no InternalsVisibleTo).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("Parlotype.Desktop.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Onboarding_WindowTitle => Get(nameof(Onboarding_WindowTitle));
    public static string Onboarding_Welcome_Title => Get(nameof(Onboarding_Welcome_Title));
    public static string Onboarding_Welcome_Body => Get(nameof(Onboarding_Welcome_Body));
    public static string Onboarding_Recording_Title => Get(nameof(Onboarding_Recording_Title));
    public static string Onboarding_Recording_Body => Get(nameof(Onboarding_Recording_Body));
    public static string Onboarding_Recording_EscLine => Get(nameof(Onboarding_Recording_EscLine));
    public static string Onboarding_Hotkeys_None => Get(nameof(Onboarding_Hotkeys_None));
    public static string Onboarding_Widget_Title => Get(nameof(Onboarding_Widget_Title));
    public static string Onboarding_Widget_Body => Get(nameof(Onboarding_Widget_Body));
    public static string Onboarding_Engine_Title => Get(nameof(Onboarding_Engine_Title));
    public static string Onboarding_Engine_Body => Get(nameof(Onboarding_Engine_Body));
    public static string Onboarding_Model_Title => Get(nameof(Onboarding_Model_Title));
    public static string Onboarding_Model_Body => Get(nameof(Onboarding_Model_Body));
    public static string Onboarding_Cloud_Title => Get(nameof(Onboarding_Cloud_Title));
    public static string Onboarding_Cloud_Body => Get(nameof(Onboarding_Cloud_Body));
    public static string Onboarding_Tray_Title => Get(nameof(Onboarding_Tray_Title));
    public static string Onboarding_Tray_Body => Get(nameof(Onboarding_Tray_Body));
    public static string Onboarding_Recap_Title => Get(nameof(Onboarding_Recap_Title));
    public static string Onboarding_Recap_Body => Get(nameof(Onboarding_Recap_Body));
    public static string Onboarding_Nav_Back => Get(nameof(Onboarding_Nav_Back));
    public static string Onboarding_Nav_Next => Get(nameof(Onboarding_Nav_Next));
    public static string Onboarding_Nav_Finish => Get(nameof(Onboarding_Nav_Finish));
    public static string Onboarding_Nav_Skip => Get(nameof(Onboarding_Nav_Skip));
    public static string Onboarding_Progress_Format => Get(nameof(Onboarding_Progress_Format));
    public static string Help_Title => Get(nameof(Help_Title));
    public static string Help_Intro => Get(nameof(Help_Intro));
    public static string Help_OpenTourButton => Get(nameof(Help_OpenTourButton));
    public static string Help_HotkeysHeading => Get(nameof(Help_HotkeysHeading));
    public static string Help_NoHotkeys => Get(nameof(Help_NoHotkeys));
    public static string Help_EscCancelLine => Get(nameof(Help_EscCancelLine));
}
