using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Base class for navigation panel sections. Each section advertises a title
/// rendered in the navigation list of the Settings window, plus grouping
/// metadata (<see cref="Category"/>) and an optional engine-scope
/// (<see cref="RestrictToEngine"/>) that hides the section when a different
/// engine is active.
/// </summary>
public abstract class SettingsSectionViewModelBase : ViewModelBase
{
    public abstract string Title { get; }

    public abstract SettingsCategory Category { get; }

    /// <summary>
    /// When set, this section is only visible while the named speech engine is
    /// active. Null means the section is always visible.
    /// </summary>
    public virtual SpeechEngine? RestrictToEngine => null;

    /// <summary>
    /// Whether this section should appear in the navigation while
    /// <paramref name="engine"/> is active. Defaults to the
    /// <see cref="RestrictToEngine"/> rule; override for visibility that
    /// depends on engine capabilities rather than a single engine identity
    /// (e.g. the Language page hides for engines with no language choices).
    /// </summary>
    public virtual bool IsVisibleFor(SpeechEngine engine) =>
        RestrictToEngine is null || RestrictToEngine == engine;
}
