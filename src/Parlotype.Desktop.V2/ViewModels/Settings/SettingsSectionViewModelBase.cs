namespace Parlotype.Desktop.V2.ViewModels.Settings;

/// <summary>
/// Base class for navigation panel sections. Each section advertises a title
/// rendered in the navigation list of the Settings window.
/// </summary>
public abstract class SettingsSectionViewModelBase : ViewModelBase
{
    public abstract string Title { get; }
}
