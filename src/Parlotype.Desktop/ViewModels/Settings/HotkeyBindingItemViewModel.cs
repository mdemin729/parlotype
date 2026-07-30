using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// One row in the hotkey list. Carries its own commands so the row template
/// needs no parent-traversal bindings.
/// </summary>
public sealed partial class HotkeyBindingItemViewModel : ObservableObject
{
    public DictationHotkey Hotkey { get; }

    public string DisplayString => Hotkey.DisplayString;

    public string ModeLabel => Hotkey.ModeLabel;

    /// <summary>
    /// Only chords work in both modes. A hold has to be push-to-talk and a
    /// double-tap has to toggle, so those rows show the mode without offering
    /// to change it.
    /// </summary>
    public bool CanChangeMode => Hotkey.Gesture.Kind == HotkeyGestureKind.Chord;

    public ICommand RemoveCommand { get; }

    public ICommand ToggleModeCommand { get; }

    public HotkeyBindingItemViewModel(
        DictationHotkey hotkey,
        ICommand removeCommand,
        ICommand toggleModeCommand)
    {
        Hotkey = hotkey;
        RemoveCommand = removeCommand;
        ToggleModeCommand = toggleModeCommand;
    }
}
