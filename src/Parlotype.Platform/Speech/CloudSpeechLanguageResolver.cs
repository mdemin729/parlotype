using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Resolves the persisted <see cref="SettingsKeys.SelectedSourceLanguage"/>
/// setting to the code a cloud STT provider's <c>language</c> form field
/// should carry, or <c>null</c> when the provider should auto-detect. Shared
/// by <see cref="OpenAiCompatibleSpeechRecognizer"/> and
/// <see cref="XaiGrokSpeechRecognizer"/> so both cloud engines apply the same
/// keyboard-layout-sentinel / auto-detect fallback policy
/// (<see cref="SourceLanguageResolver"/>) that <c>AudioPipelineService</c>
/// applies for Whisper.
/// </summary>
internal static class CloudSpeechLanguageResolver
{
    /// <summary>
    /// Returns the concrete ISO-639-1 (or provider-specific) language code to
    /// send, or <c>null</c> when the resolved source is auto-detect.
    /// </summary>
    internal static async Task<string?> ResolveAsync(
        ISettingsService settings,
        IKeyboardLayoutService keyboardLayout,
        IReadOnlyList<LanguageInfo>? supported,
        CancellationToken cancellationToken)
    {
        var sourceCode = await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, cancellationToken);
        var detectedLayout = LanguageCatalog.IsKeyboardLayout(sourceCode) ? keyboardLayout.Detect() : null;
        var resolved = SourceLanguageResolver.Resolve(sourceCode, detectedLayout, supported);

        return LanguageCatalog.IsAutoDetect(resolved) ? null : resolved;
    }
}
