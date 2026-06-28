namespace Parlotype.Core.Settings;

/// <summary>Well-known settings keys.</summary>
public static class SettingsKeys
{
    public const string SelectedMicrophoneId = "SelectedMicrophoneId";
    public const string SelectedTheme = "SelectedTheme";
    public const string SelectedWhisperModel = "SelectedWhisperModel";
    public const string HotkeyModifiers = "HotkeyModifiers";
    public const string HotkeyKey = "HotkeyKey";
    public const string ActivationMode = "ActivationMode";
    public const string RuntimePreference = "RuntimePreference";
    public const string WaitTime = "WaitTime";
    public const string AutomaticPunctuation = "AutomaticPunctuation";
    public const string FilterProfanity = "FilterProfanity";

    /// <summary>
    /// Opt-in toggle for warming the speech model in the background at app
    /// startup so the first record press is instant. Default false — when unset
    /// or false, no prewarm runs and the model loads on first use (ADR-038).
    /// </summary>
    public const string PrewarmModelOnStartup = "PrewarmModelOnStartup";

    /// <summary>
    /// Legacy Whisper-only translate-to-English flag (ADR-021). Superseded by
    /// <see cref="TranslationEnabled"/> + <see cref="SelectedTargetLanguage"/>.
    /// Still read once on startup so existing installations migrate cleanly; no
    /// code path should write it after migration.
    /// </summary>
    public const string TranslateToEnglish = "TranslateToEnglish";

    public const string SelectedSourceLanguage = "SelectedSourceLanguage";
    public const string SelectedTargetLanguage = "SelectedTargetLanguage";

    /// <summary>
    /// Master on/off for transcription translation. When false the pipeline
    /// emits the source language verbatim regardless of
    /// <see cref="SelectedTargetLanguage"/>.
    /// </summary>
    public const string TranslationEnabled = "TranslationEnabled";

    /// <summary>
    /// Legacy shared most-recently-used languages list. Superseded by
    /// <see cref="RecentSourceLanguages"/> and <see cref="RecentTargetLanguages"/>.
    /// Read once on startup as the seed for the new source-side MRU; not written
    /// after migration.
    /// </summary>
    public const string RecentLanguages = "RecentLanguages";

    public const string RecentSourceLanguages = "RecentSourceLanguages";
    public const string RecentTargetLanguages = "RecentTargetLanguages";

    public const string SpeechEngine = "SpeechEngine";
    public const string SelectedGemma4Model = "SelectedGemma4Model";
    public const string LlamaCppServerFolder = "LlamaCppServerFolder";
    public const string LlamaCppPort = "LlamaCppPort";
    public const string LlamaCppActiveInstall = "LlamaCppActiveInstall";
    public const string ActivePromptId = "ActivePromptId";
}
