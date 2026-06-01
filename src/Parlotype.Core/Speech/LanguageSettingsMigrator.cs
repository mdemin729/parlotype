using Parlotype.Core.Settings;

namespace Parlotype.Core.Speech;

/// <summary>
/// One-shot migration from the pre-redesign translation/recent-language settings
/// to the new model (<see cref="SettingsKeys.TranslationEnabled"/> +
/// <see cref="SettingsKeys.SelectedTargetLanguage"/>; per-role MRU lists).
///
/// <para>Idempotent — guarded by the presence of
/// <see cref="SettingsKeys.TranslationEnabled"/>. Once that key is written
/// (whether <c>true</c> or <c>false</c>), <see cref="MigrateAsync"/> short-circuits
/// after a single settings read.</para>
/// </summary>
public static class LanguageSettingsMigrator
{
    public static async Task MigrateAsync(ISettingsService settings, CancellationToken ct = default)
    {
        var enabledStr = await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, ct).ConfigureAwait(false);
        if (bool.TryParse(enabledStr, out _))
            return;

        await MigrateTranslationAsync(settings, ct).ConfigureAwait(false);
        await MigrateRecentLanguagesAsync(settings, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Derives <see cref="SettingsKeys.TranslationEnabled"/> from the legacy
    /// <see cref="SettingsKeys.TranslateToEnglish"/> flag and/or an explicit
    /// <see cref="SettingsKeys.SelectedTargetLanguage"/>. Preserves user intent on
    /// first launch after upgrade for both engines.
    /// </summary>
    private static async Task MigrateTranslationAsync(ISettingsService settings, CancellationToken ct)
    {
        var legacyStr = await settings.GetAsync<string>(SettingsKeys.TranslateToEnglish, ct).ConfigureAwait(false);
        var legacyTranslate = bool.TryParse(legacyStr, out var le) && le;

        var existingTarget = await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, ct).ConfigureAwait(false);
        var hasExplicitTarget = !string.IsNullOrWhiteSpace(existingTarget)
            && !LanguageCatalog.IsNoTranslation(existingTarget);

        // Treat the user as wanting translation if either (a) they ticked the legacy
        // Whisper toggle, or (b) they picked an explicit non-"none" target via the
        // Gemma 4 picker. Both pre-dated TranslationEnabled.
        var enabled = legacyTranslate || hasExplicitTarget;

        await settings.SetAsync(SettingsKeys.TranslationEnabled, enabled.ToString(), ct).ConfigureAwait(false);

        // Legacy Whisper toggle was on but no target was set ⇒ English is the implied target.
        if (legacyTranslate && !hasExplicitTarget)
        {
            await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, LanguageCatalog.EnglishCode, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Seeds <see cref="SettingsKeys.RecentSourceLanguages"/> from the legacy shared
    /// <see cref="SettingsKeys.RecentLanguages"/> list. The target MRU starts empty —
    /// the shared list previously mixed both roles, so attributing it to one side
    /// (source) preserves the more useful signal.
    /// </summary>
    private static async Task MigrateRecentLanguagesAsync(ISettingsService settings, CancellationToken ct)
    {
        var legacy = await settings.GetAsync<List<string>>(SettingsKeys.RecentLanguages, ct).ConfigureAwait(false);
        if (legacy is null || legacy.Count == 0)
            return;

        var newSource = await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages, ct).ConfigureAwait(false);
        if (newSource is { Count: > 0 })
            return;

        await settings.SetAsync(SettingsKeys.RecentSourceLanguages, legacy, ct).ConfigureAwait(false);
    }
}
