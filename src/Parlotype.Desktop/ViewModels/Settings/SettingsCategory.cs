using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Top-level grouping for sections in the Settings window navigation pane.
/// Order of enum values controls the visual order of group headers.
/// </summary>
public enum SettingsCategory
{
    Audio,
    SpeechEngine,
    Input,
    Appearance,
    Application,
}

public static class SettingsCategoryExtensions
{
    public static string GetDisplayName(this SettingsCategory category) => category switch
    {
        SettingsCategory.Audio => Strings.Settings_Category_Audio,
        SettingsCategory.SpeechEngine => Strings.Settings_Category_SpeechEngine,
        SettingsCategory.Input => Strings.Settings_Category_Input,
        SettingsCategory.Appearance => Strings.Settings_Category_Appearance,
        SettingsCategory.Application => Strings.Settings_Category_Application,
        _ => category.ToString(),
    };
}
