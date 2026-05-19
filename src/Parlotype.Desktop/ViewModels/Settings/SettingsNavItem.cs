namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// One row in the Settings window's left navigation list. Either a
/// non-selectable group header (<see cref="IsHeader"/> true, <see cref="Section"/>
/// null) or a section row pointing at a concrete section ViewModel.
/// </summary>
public sealed class SettingsNavItem
{
    public required string Label { get; init; }
    public required bool IsHeader { get; init; }
    public SettingsSectionViewModelBase? Section { get; init; }

    public static SettingsNavItem Header(string label) =>
        new() { Label = label, IsHeader = true, Section = null };

    public static SettingsNavItem ForSection(SettingsSectionViewModelBase section) =>
        new() { Label = section.Title, IsHeader = false, Section = section };
}
