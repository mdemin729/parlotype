using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Localization;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Settings → Appearance → Interface language (ADR-064). Switching takes effect
/// immediately; there is no restart prompt.
/// </summary>
public partial class InterfaceLanguageSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly UiLanguageService _uiLanguage;
    private readonly ILogger<InterfaceLanguageSettingsViewModel> _logger;

    public override string Title => Strings.Settings_InterfaceLanguage_Title;
    public override SettingsCategory Category => SettingsCategory.Appearance;

    public UiLanguageDisplayItem[] LanguageOptions { get; }

    [ObservableProperty]
    private string _selectedSettingValue = SupportedUiLanguages.SystemSettingValue;

    public InterfaceLanguageSettingsViewModel(
        UiLanguageService uiLanguage,
        ILogger<InterfaceLanguageSettingsViewModel>? logger = null)
    {
        _uiLanguage = uiLanguage;
        _logger = logger ?? NullLogger<InterfaceLanguageSettingsViewModel>.Instance;

        LanguageOptions =
        [
            new(SupportedUiLanguages.SystemSettingValue, Strings.Settings_InterfaceLanguage_System, SelectLanguageCommand),
            .. SupportedUiLanguages.All.Select(l =>
                new UiLanguageDisplayItem(l.CultureName, l.EndonymName, SelectLanguageCommand)),
        ];

        RefreshLabels();
        UpdateSelection(SelectedSettingValue);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        SelectedSettingValue = await _uiLanguage.GetStoredSettingValueAsync();
        UpdateSelection(SelectedSettingValue);
    }

    [RelayCommand]
    private async Task SelectLanguageAsync(string settingValue)
    {
        _logger.LogInformation("Interface language selected: {SettingValue}", settingValue);
        SelectedSettingValue = settingValue;
        UpdateSelection(settingValue);
        await _uiLanguage.SetLanguageAsync(settingValue);
    }

    /// <summary>
    /// The "System default" row carries copy built here — its label and the
    /// detail line naming what the system currently resolves to — so it has to be
    /// rewritten rather than rebound.
    /// </summary>
    protected override void OnCultureChanged()
    {
        base.OnCultureChanged();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var system = LanguageOptions[0];
        system.Label = Strings.Settings_InterfaceLanguage_System;
        system.Detail = _uiLanguage.SystemLanguage is { } shipped
            ? Strings.Format_Settings_InterfaceLanguage_SystemDetailFormat(shipped.EndonymName)
            : Strings.Settings_InterfaceLanguage_SystemDetailUnsupported;

        // Every other row is an endonym and stays put by design — "Русский" reads
        // the same from a Spanish interface as from an English one.
    }

    private void UpdateSelection(string settingValue)
    {
        foreach (var option in LanguageOptions)
            option.IsSelected = option.SettingValue == settingValue;
    }
}
