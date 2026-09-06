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

    /// <summary>Localized (ADR-064) — see <see cref="HotkeyText"/>.</summary>
    public string DisplayString => HotkeyText.Gesture(Hotkey.Gesture);

    public string ModeLabel => HotkeyText.Mode(Hotkey.Mode);

    public ICommand AddCommand { get; }

    public HotkeyPresetViewModel(DictationHotkey hotkey, ICommand addCommand)
    {
        Hotkey = hotkey;
        AddCommand = addCommand;
    }
}
