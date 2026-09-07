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

    /// <summary>
    /// Localized (ADR-064). Core's <c>Hotkey.DisplayString</c> is the invariant
    /// form the logs use; this is the one the window shows.
    /// </summary>
    public string DisplayString => HotkeyText.Gesture(Hotkey.Gesture);

    public string ModeLabel => HotkeyText.Mode(Hotkey.Mode);

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

    /// <summary>
    /// Re-raises the localized display strings after an interface-language
    /// switch (ADR-064 amendment). <see cref="DisplayString"/>/<see cref="ModeLabel"/>
    /// already recompute on every read; nothing tells a bound view to re-read
    /// them without this.
    /// </summary>
    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(DisplayString));
        OnPropertyChanged(nameof(ModeLabel));
    }
}
