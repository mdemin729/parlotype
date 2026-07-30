using System.Windows.Input;
using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// A ready-made gesture in the Add menu. Carries its own command because the
/// menu is a flyout, which sits outside the visual tree its bindings would
/// otherwise have to traverse.
/// </summary>
public sealed class HotkeyPresetViewModel
{
    public DictationHotkey Hotkey { get; }

    public string DisplayString => Hotkey.DisplayString;

    public string ModeLabel => Hotkey.ModeLabel;

    public ICommand AddCommand { get; }

    public HotkeyPresetViewModel(DictationHotkey hotkey, ICommand addCommand)
    {
        Hotkey = hotkey;
        AddCommand = addCommand;
    }
}
