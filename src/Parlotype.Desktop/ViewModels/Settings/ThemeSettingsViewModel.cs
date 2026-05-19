using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class ThemeSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILogger<ThemeSettingsViewModel> _logger;

    public override string Title => "Theme";
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
            new(AppTheme.Default, "Default (system)", SelectThemeCommand),
            new(AppTheme.Light, "Light", SelectThemeCommand),
            new(AppTheme.Dark, "Dark", SelectThemeCommand),
        ];

        _ = InitializeAsync();
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
