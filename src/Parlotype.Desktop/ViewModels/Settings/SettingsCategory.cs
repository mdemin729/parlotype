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
}

public static class SettingsCategoryExtensions
{
    public static string GetDisplayName(this SettingsCategory category) => category switch
    {
        SettingsCategory.Audio => "Audio",
        SettingsCategory.SpeechEngine => "Speech engine",
        SettingsCategory.Input => "Input",
        SettingsCategory.Appearance => "Appearance",
        _ => category.ToString(),
    };
}
