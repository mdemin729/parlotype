namespace Parlotype.Core.Settings;

/// <summary>Well-known settings keys.</summary>
public static class SettingsKeys
{
    public const string SelectedMicrophoneId = "SelectedMicrophoneId";
    public const string SelectedTheme = "SelectedTheme";
    public const string SelectedWhisperModel = "SelectedWhisperModel";
    /// <summary>
    /// Legacy single-chord hotkey settings (modifiers + key + one global
    /// activation mode). Superseded by <see cref="HotkeyBindings"/>, which holds
    /// several gestures at once. Read one final time by
    /// <see cref="Hotkeys.HotkeySettingsMigrator"/> so an upgrade keeps the
    /// user's own shortcut; nothing writes them afterwards.
    /// </summary>
    public const string HotkeyModifiers = "HotkeyModifiers";

    /// <inheritdoc cref="HotkeyModifiers"/>
    public const string HotkeyKey = "HotkeyKey";

    /// <inheritdoc cref="HotkeyModifiers"/>
    public const string ActivationMode = "ActivationMode";

    /// <summary>
    /// The configured dictation hotkeys, encoded by
    /// <see cref="Hotkeys.HotkeyBindingCodec"/> — e.g.
    /// <c>["hold|Ctrl|Right|PushToTalk", "doubletap|Ctrl|Either|Toggle"]</c>.
    /// Several gestures can be bound at once; see
    /// <see cref="Hotkeys.DictationHotkeyDefaults"/> for what ships by default.
    /// </summary>
    public const string HotkeyBindings = "HotkeyBindings";
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
    public const string SelectedParakeetModel = "SelectedParakeetModel";
    public const string LlamaCppServerFolder = "LlamaCppServerFolder";
    public const string LlamaCppPort = "LlamaCppPort";
    public const string LlamaCppActiveInstall = "LlamaCppActiveInstall";
    public const string ActivePromptId = "ActivePromptId";

    /// <summary>
    /// Base URL for the OpenAI-compatible cloud transcription provider
    /// (<see cref="Speech.SpeechEngine.OpenAiCompatible"/>, ADR-032). Defaults to
    /// <c>https://api.openai.com/v1</c> when unset; pointing this at another host
    /// (e.g. Groq) is how the same engine serves multiple OpenAI-protocol providers.
    /// </summary>
    public const string OpenAiCompatBaseUrl = "OpenAiCompatBaseUrl";

    /// <summary>
    /// Model id sent to the OpenAI-compatible transcription endpoint (e.g.
    /// <c>gpt-4o-mini-transcribe</c>, <c>whisper-1</c>). Defaults to
    /// <c>gpt-4o-mini-transcribe</c> when unset.
    /// </summary>
    public const string OpenAiCompatModel = "OpenAiCompatModel";

    /// <summary>
    /// Base URL for the xAI Grok cloud transcription provider
    /// (<see cref="Speech.SpeechEngine.XaiGrok"/>, ADR-032). Defaults to
    /// <c>https://api.x.ai/v1</c> when unset.
    /// </summary>
    public const string XaiGrokBaseUrl = "XaiGrokBaseUrl";

    /// <summary>
    /// Model id sent to the xAI Grok transcription endpoint. Defaults to
    /// <c>grok-stt</c> when unset.
    /// </summary>
    public const string XaiGrokModel = "XaiGrokModel";

    /// <summary>
    /// <see cref="ISecretStore"/> key name for the OpenAI-compatible provider's
    /// API key. The value itself lives in <see cref="ISecretStore"/>
    /// (encrypted at rest where the OS supports it), never in settings.json —
    /// this constant is only the lookup key.
    /// </summary>
    public const string OpenAiCompatApiKey = "OpenAiCompatApiKey";

    /// <summary>
    /// <see cref="ISecretStore"/> key name for the xAI Grok provider's API key.
    /// The value itself lives in <see cref="ISecretStore"/> (encrypted at rest
    /// where the OS supports it), never in settings.json — this constant is
    /// only the lookup key.
    /// </summary>
    public const string XaiGrokApiKey = "XaiGrokApiKey";

    /// <summary>
    /// Whether Parlotype checks the release feed for updates on its own.
    /// Defaults to <c>true</c> when unset (ADR-053): the check is an
    /// unauthenticated read of a public endpoint that sends no identifying
    /// information, and users who never check will silently run builds with
    /// known bugs. Setting it false stops all outbound traffic from the updater;
    /// "Check now" still works and remains explicitly user-driven.
    /// </summary>
    public const string UpdatesCheckAutomatically = "UpdatesCheckAutomatically";

    /// <summary>
    /// Round-trip ("O") timestamp of the last time the release feed was reached
    /// successfully. Shown in Settings so the user can see how much the updater
    /// has actually talked to the network. Not sent anywhere.
    /// </summary>
    public const string UpdatesLastCheckedUtc = "UpdatesLastCheckedUtc";

    /// <summary>
    /// Whether uninstalling Parlotype also deletes <see cref="IAppPaths.DataDirectory"/>
    /// — downloaded models, settings, and stored API keys. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Velopack's uninstall hook may not show UI, so consent for a destructive
    /// action cannot be obtained at uninstall time. This key <em>is</em> that
    /// consent, recorded in advance from Settings → Application → Data; the hook
    /// only executes a decision the user already made (ADR-053). Default off
    /// because many uninstalls are really troubleshooting reinstalls, where
    /// discarding several GB of models is the wrong outcome.
    /// </remarks>
    public const string UninstallRemovesUserData = "UninstallRemovesUserData";

    /// <summary>
    /// Whether the first-run onboarding tour has already been offered
    /// (ADR-056). Stored as a string bool; unset or unparsable means "not yet"
    /// and the tour auto-opens once. Written <c>"True"</c> at the moment the
    /// tour is shown — not when it finishes — so a crash mid-tour still counts
    /// as offered. The tour stays reachable from Settings → Help afterwards.
    /// </summary>
    public const string OnboardingCompleted = "OnboardingCompleted";
}
