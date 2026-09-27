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

    /// <summary>
    /// The "System default" row, held apart from the list. It names a behaviour,
    /// not a language, and at 25 languages it stops reading as one if it just
    /// sits at the top of the same list.
    /// </summary>
    public UiLanguageDisplayItem SystemOption { get; }

    /// <summary>
    /// The shipping languages, sorted by endonym.
    /// </summary>
    /// <remarks>
    /// Sorted under the <em>invariant</em> culture, deliberately, not the current
    /// UI culture: a list that reshuffles itself every time the user switches
    /// language makes the row they just clicked jump out from under the cursor.
    /// Sorting by endonym rather than English name is the other half of the same
    /// idea — someone stranded in a language they cannot read is scanning for
    /// their own language's spelling, not for its English name.
    /// </remarks>
    public UiLanguageDisplayItem[] LanguageOptions { get; }

    [ObservableProperty]
    private string _selectedSettingValue = SupportedUiLanguages.SystemSettingValue;

    public InterfaceLanguageSettingsViewModel(
        UiLanguageService uiLanguage,
        ILogger<InterfaceLanguageSettingsViewModel>? logger = null)
    {
        _uiLanguage = uiLanguage;
        _logger = logger ?? NullLogger<InterfaceLanguageSettingsViewModel>.Instance;

        SystemOption = new(
            SupportedUiLanguages.SystemSettingValue,
            Strings.Settings_InterfaceLanguage_System,
            SelectLanguageCommand);

        LanguageOptions = SupportedUiLanguages.All
            .OrderBy(l => l.EndonymName, StringComparer.InvariantCulture)
            .Select(l => new UiLanguageDisplayItem(l.CultureName, l.EndonymName, SelectLanguageCommand))
            .ToArray();

        RefreshLabels();
        UpdateSelection(SelectedSettingValue);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        SelectedSettingValue = await _uiLanguage.GetStoredSettingValueAsync();
        UpdateSelection(SelectedSettingValue);
    }

    /// <summary>
    /// Synchronous on purpose. Every row's Button binds the same
    /// <c>SelectLanguageCommand</c> instance; had this been <c>async Task</c>,
    /// CommunityToolkit.Mvvm's generated <c>AsyncRelayCommand</c> would set
    /// <c>CanExecute = false</c> for the whole time the command is running (the
    /// default unless <c>AllowConcurrentExecutions</c> is set), which disables
    /// every button sharing it — all four rows would flash disabled/re-enabled
    /// for the length of the settings write, which is exactly the flicker this
    /// fixes. See <c>memory/knowledge/asyncrelaycommand-flicker.md</c>.
    /// </summary>
    [RelayCommand]
    private void SelectLanguage(string settingValue)
    {
        _logger.LogInformation("Interface language selected: {SettingValue}", settingValue);
        SelectedSettingValue = settingValue;
        UpdateSelection(settingValue);
        _ = _uiLanguage.SetLanguageAsync(settingValue);
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
        SystemOption.Label = Strings.Settings_InterfaceLanguage_System;
        SystemOption.Detail = _uiLanguage.SystemLanguage is { } shipped
            ? Strings.Format_Settings_InterfaceLanguage_SystemDetailFormat(shipped.EndonymName)
            : Strings.Settings_InterfaceLanguage_SystemDetailUnsupported;

        // Every other row is an endonym and stays put by design — "Русский" reads
        // the same from a Spanish interface as from an English one.
    }

    private void UpdateSelection(string settingValue)
    {
        SystemOption.IsSelected = SystemOption.SettingValue == settingValue;
        foreach (var option in LanguageOptions)
            option.IsSelected = option.SettingValue == settingValue;
    }
}
