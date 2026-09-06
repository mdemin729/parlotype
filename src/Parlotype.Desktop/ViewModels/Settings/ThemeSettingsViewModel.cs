using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class ThemeSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILogger<ThemeSettingsViewModel> _logger;

    public override string Title => Strings.Settings_Theme_Title;
    public override SettingsCategory Category => SettingsCategory.Appearance;

    public ThemeDisplayItem[] ThemeOptions { get; }

    [ObservableProperty]
    private AppTheme _selectedTheme;

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeSettingsViewModel(
        ISettingsService settings,
        ILogger<ThemeSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? NullLogger<ThemeSettingsViewModel>.Instance;

        ThemeOptions =
        [
            new(AppTheme.Default, Strings.Settings_Theme_Default, SelectThemeCommand),
            new(AppTheme.Light, Strings.Settings_Theme_Light, SelectThemeCommand),
            new(AppTheme.Dark, Strings.Settings_Theme_Dark, SelectThemeCommand),
        ];

        _ = InitializeAsync();
    }

    /// <summary>
    /// The option labels are built here rather than bound, so the base class's
    /// Title refresh is not enough — rewrite them too (ADR-064).
    /// </summary>
    protected override void OnCultureChanged()
    {
        base.OnCultureChanged();

        foreach (var option in ThemeOptions)
        {
            option.DisplayName = option.Theme switch
            {
                AppTheme.Light => Strings.Settings_Theme_Light,
                AppTheme.Dark => Strings.Settings_Theme_Dark,
                _ => Strings.Settings_Theme_Default,
            };
        }
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedTheme);
        if (Enum.TryParse<AppTheme>(saved, out var theme))
            SelectedTheme = theme;
    }

    [RelayCommand]
    private void SelectTheme(AppTheme theme)
    {
        _logger.LogInformation("Theme selected: {Theme}", theme);
        SelectedTheme = theme;
        ThemeChanged?.Invoke(this, theme);
        _ = _settings.SetAsync(SettingsKeys.SelectedTheme, theme.ToString());
    }
}
