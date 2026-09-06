using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Localization;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Decides which language the interface is drawn in and applies it (ADR-064).
/// The DI-visible seam over <see cref="Localizer"/>, which is a singleton because
/// XAML markup extensions cannot reach a container.
/// </summary>
public sealed class UiLanguageService(
    ISettingsService settings,
    ILogger<UiLanguageService>? logger = null)
{
    private readonly ILogger<UiLanguageService> _logger = logger ?? NullLogger<UiLanguageService>.Instance;

    /// <summary>
    /// The interface language the operating system asks for, or
    /// <see langword="null"/> when Parlotype ships nothing for it and English is
    /// what "System default" resolves to.
    /// </summary>
    public UiLanguage? SystemLanguage =>
        SupportedUiLanguages.MatchSystemCulture(CultureInfo.InstalledUICulture.Name);

    /// <summary>
    /// Reads the stored preference without touching the culture. Separate from
    /// <see cref="ApplyLanguage"/> so startup can do the file read off the UI
    /// thread and the culture change on it — see <c>App.OnFrameworkInitializationCompleted</c>.
    /// </summary>
    public Task<string?> ReadStoredLanguageAsync(CancellationToken cancellationToken = default) =>
        settings.GetAsync<string>(SettingsKeys.UiLanguage, cancellationToken);

    /// <summary>
    /// Applies <paramref name="settingValue"/> to the calling thread and to every
    /// thread started afterwards. Call on the UI thread: the culture has to land
    /// on the thread that reads resources.
    /// </summary>
    public void ApplyLanguage(string? settingValue) => Apply(settingValue);

    /// <summary>
    /// Reads the stored preference and applies it, on whatever thread the read
    /// completes on. Convenient for tests and non-UI callers; startup uses the
    /// two halves separately.
    /// </summary>
    public async Task ApplyStartupLanguageAsync(CancellationToken cancellationToken = default) =>
        Apply(await ReadStoredLanguageAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Applies <paramref name="settingValue"/> — a culture name, or
    /// <see cref="SupportedUiLanguages.SystemSettingValue"/> / null to follow the
    /// operating system — and persists it.
    /// </summary>
    public async Task SetLanguageAsync(string settingValue, CancellationToken cancellationToken = default)
    {
        Apply(settingValue);
        await settings.SetAsync(SettingsKeys.UiLanguage, settingValue, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The stored value the settings picker should show as selected.</summary>
    public async Task<string> GetStoredSettingValueAsync(CancellationToken cancellationToken = default)
    {
        var stored = await ReadStoredLanguageAsync(cancellationToken).ConfigureAwait(false);

        return SupportedUiLanguages.ResolveSettingValue(stored) ?? SupportedUiLanguages.SystemSettingValue;
    }

    private void Apply(string? settingValue)
    {
        var explicitCulture = SupportedUiLanguages.ResolveSettingValue(settingValue);

        if (explicitCulture is not null)
        {
            _logger.LogInformation("Interface language: {Culture} (chosen in settings)", explicitCulture);
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo(explicitCulture));
            return;
        }

        // Following the system. Hand .NET the installed UI culture verbatim rather
        // than the neutral language we matched: resource fallback walks ru-RU → ru
        // → neutral by itself, and keeping the region means a future ru-UA or
        // es-MX satellite would be picked up without changing this code.
        var system = CultureInfo.InstalledUICulture;
        var shipped = SystemLanguage;

        _logger.LogInformation(
            "Interface language: following the system ({System}) — {Resolution}",
            system.Name,
            shipped is null ? "not translated, using English" : $"using {shipped.CultureName}");

        Localizer.Instance.SetCulture(shipped is null ? CultureInfo.GetCultureInfo(SupportedUiLanguages.NeutralCultureName) : system);
    }
}
