using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;

namespace Parlotype.WinUI.ViewModels;

/// <summary>Display item for a theme choice.</summary>
public record ThemeItem(AppTheme Theme, string DisplayName, string Description, string IconGlyph);

/// <summary>
/// ViewModel for the Appearance settings page.
/// Manages theme selection and persistence.
/// </summary>
public partial class AppearanceViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILogger<AppearanceViewModel> _logger;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    /// <summary>Raised when the user selects a new theme.</summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>Available theme choices for the UI.</summary>
    public static ThemeItem[] ThemeItems { get; } =
    [
        new(AppTheme.Default, "System Default", "Follow the Windows theme setting", "\uE770"),
        new(AppTheme.Light,   "Light",          "Always use light theme",           "\uE706"),
        new(AppTheme.Dark,    "Dark",           "Always use dark theme",            "\uE708"),
    ];

    public AppearanceViewModel(
        ISettingsService settings,
        ILogger<AppearanceViewModel>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? NullLogger<AppearanceViewModel>.Instance;
    }

    /// <summary>Loads the persisted theme setting.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedTheme);
            SelectedTheme = Enum.TryParse<AppTheme>(saved, ignoreCase: true, out var parsed)
                ? parsed
                : AppTheme.Default;
            _logger.LogDebug("Loaded theme: {Theme}", SelectedTheme);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load theme setting, defaulting to System");
            SelectedTheme = AppTheme.Default;
        }
    }

    [RelayCommand]
    private async Task SelectThemeAsync(AppTheme theme)
    {
        SelectedTheme = theme;

        try
        {
            await _settings.SetAsync(SettingsKeys.SelectedTheme, theme.ToString());
            _logger.LogInformation("Theme changed to {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist theme setting");
        }

        ThemeChanged?.Invoke(this, theme);
    }
}
