using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// A ready-made gesture in the Add menu. Carries its own command because the
/// menu is a flyout, which sits outside the visual tree its bindings would
/// otherwise have to traverse.
/// </summary>
/// <remarks>
/// <see cref="ObservableObject"/> even though every property here is a plain
/// computed getter: the "Add" button's <c>MenuFlyout</c> is built once and its
/// item containers are reused across opens, so without
/// <see cref="INotifyPropertyChanged"/> a bound label never re-reads
/// <see cref="DisplayString"/>/<see cref="ModeLabel"/> after the first render —
/// an interface-language switch would leave every preset in the old language
/// (ADR-064 amendment).
/// </remarks>
public sealed class HotkeyPresetViewModel : ObservableObject
{
    public DictationHotkey Hotkey { get; }

    /// <summary>Localized (ADR-064) — see <see cref="HotkeyText"/>.</summary>
    public string DisplayString => HotkeyText.Gesture(Hotkey.Gesture);

    public string ModeLabel => HotkeyText.Mode(Hotkey.Mode);

    public ICommand AddCommand { get; }

    public HotkeyPresetViewModel(DictationHotkey hotkey, ICommand addCommand)
    {
        Hotkey = hotkey;
        AddCommand = addCommand;
    }

    /// <summary>Re-raises the localized display strings after an interface-language switch.</summary>
    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(DisplayString));
        OnPropertyChanged(nameof(ModeLabel));
    }
}
