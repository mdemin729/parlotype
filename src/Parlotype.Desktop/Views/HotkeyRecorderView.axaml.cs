using Avalonia.Controls;
using Avalonia.Input;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views;

public partial class HotkeyRecorderView : UserControl
{
    public HotkeyRecorderView()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not HotkeyRecorderViewModel vm || !vm.IsRecording)
            return;

        // Ignore standalone modifier presses — wait for the primary key
        if (IsModifierKey(e.Key))
            return;

        var keyName = MapAvaloniaKey(e.Key);
        if (keyName is null)
            return;

        var modifiers = HotkeyModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            modifiers |= HotkeyModifiers.Ctrl;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            modifiers |= HotkeyModifiers.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            modifiers |= HotkeyModifiers.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            modifiers |= HotkeyModifiers.Meta;

        var binding = new HotkeyBinding(modifiers, keyName);
        vm.ApplyRecordedBinding(binding);
        e.Handled = true;
    }

    protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        if (DataContext is HotkeyRecorderViewModel { IsRecording: true } vm)
            vm.StopRecordingCommand.Execute(null);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private static string? MapAvaloniaKey(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Return => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Escape",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.PrintScreen => "PrintScreen",
        Key.Pause => "Pause",

        >= Key.F1 and <= Key.F12 => $"F{(int)key - (int)Key.F1 + 1}",
        >= Key.A and <= Key.Z => ((char)('A' + key - Key.A)).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + key - Key.D0)).ToString(),

        Key.OemMinus => "Minus",
        Key.OemPlus => "Equals",
        Key.OemTilde => "BackQuote",
        Key.OemComma => "Comma",
        Key.OemPeriod => "Period",
        Key.OemQuestion => "Slash",
        Key.OemSemicolon => "Semicolon",
        Key.OemQuotes => "Quote",
        Key.OemBackslash or Key.OemPipe => "Backslash",

        _ => null
    };
}
