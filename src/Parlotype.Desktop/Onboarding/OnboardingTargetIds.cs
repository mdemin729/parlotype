using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// Well-known <see cref="OnboardingTarget"/> ids. The wizard's step definitions
/// and the AXAML markers must agree on these strings; keeping them as constants
/// in one place is what makes that agreement compile-checked on the C# side
/// (AXAML references them via <c>{x:Static}</c>).
/// </summary>
public static class OnboardingTargetIds
{
    public const string TranscribeRecord = "Transcribe.Record";
    public const string TranscribeGrip = "Transcribe.Grip";
    public const string TranscribeClose = "Transcribe.Close";
    public const string TranscribeLanguageStrip = "Transcribe.LanguageStrip";

    /// <summary>The engine cards list on the Engine settings page.</summary>
    public const string SettingsEngineList = "Settings.EngineList";

    /// <summary>
    /// The model list on whichever per-engine model page is open. All three
    /// local-engine model views carry the same id — at most one is visible at a
    /// time because the pages are engine-restricted.
    /// </summary>
    public const string SettingsModelList = "Settings.ModelList";

    /// <summary>Per-engine card on the Engine page, e.g. <c>Settings.EngineCard.XaiGrok</c>.</summary>
    public static string EngineCard(SpeechEngine engine) => $"Settings.EngineCard.{engine}";
}
