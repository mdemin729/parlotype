using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class LanguageSettingsMigratorTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Store { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            Store[key] = value;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Migrate_FromLegacyTranslateOn_NoTarget_SetsEnabledAndEnglishTarget()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString());

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Equal("en", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_FromLegacyTranslateOff_SetsEnabledFalse_DoesNotTouchTarget()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, false.ToString());

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Null(await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_FromExplicitTarget_OnlyOn_Gemma4Path_SetsEnabledTrue()
    {
        // Pre-redesign Gemma 4 user: picked an explicit non-"none" target, never
        // touched the Whisper TranslateToEnglish flag. Migration must read that
        // as "translation intended" and flip TranslationEnabled on.
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr");

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Equal("fr", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_FromNoneTarget_AndLegacyOff_SetsEnabledFalse()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, LanguageCatalog.NoTranslationCode);

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Equal(LanguageCatalog.NoTranslationCode,
            await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_EmptySettings_SetsEnabledFalse()
    {
        var settings = new FakeSettingsService();

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Null(await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_IsIdempotent_DoesNotOverwriteNewValues()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr");
        // Legacy flag also present but should be ignored once new key exists.
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, false.ToString());

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Equal("fr", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task Migrate_SeedsRecentSourceFromLegacySharedList()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RecentLanguages, new List<string> { "fr", "ru", "de" });

        await LanguageSettingsMigrator.MigrateAsync(settings);

        var migrated = await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages);
        Assert.NotNull(migrated);
        Assert.Equal(new[] { "fr", "ru", "de" }, migrated);

        // Target MRU is intentionally left empty — the legacy shared list mixed
        // both roles and can't be attributed cleanly to the target side.
        Assert.Null(await settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages));
    }

    [Fact]
    public async Task Migrate_LeavesRecentsAlone_WhenNewSourceListAlreadySet()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RecentLanguages, new List<string> { "fr", "ru" });
        await settings.SetAsync(SettingsKeys.RecentSourceLanguages, new List<string> { "es" });

        await LanguageSettingsMigrator.MigrateAsync(settings);

        var current = await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages);
        Assert.Equal(new[] { "es" }, current);
    }

    [Fact]
    public async Task Migrate_SkipsRecentMigration_WhenLegacyListIsEmpty()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RecentLanguages, new List<string>());

        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Null(await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages));
    }

    [Fact]
    public async Task Migrate_DoesNotWriteTwice_OnSecondCall()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString());

        await LanguageSettingsMigrator.MigrateAsync(settings);
        // Tamper with the migrated value, then re-run — migration must early-return.
        await settings.SetAsync(SettingsKeys.TranslationEnabled, false.ToString());
        await LanguageSettingsMigrator.MigrateAsync(settings);

        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
    }
}
