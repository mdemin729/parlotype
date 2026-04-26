using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Parlotype.Core.Hotkeys;
using Parlotype.WinUI.ViewModels;
using Windows.System;

namespace Parlotype.WinUI.Views;

public sealed partial class HotkeySettingsPage : Page
{
    public HotkeySettingsViewModel ViewModel { get; }

    public HotkeySettingsPage()
    {
        ViewModel = new HotkeySettingsViewModel();
        InitializeComponent();

        KeyDown += OnPageKeyDown;
        LostFocus += OnPageLostFocus;
    }

    public HotkeySettingsPage(HotkeySettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        KeyDown += OnPageKeyDown;
        LostFocus += OnPageLostFocus;
    }

    // ── Hotkey recorder button ───────────────────────────────────────

    private void RecorderButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.IsRecording)
        {
            ViewModel.StopRecordingCommand.Execute(null);
        }
        else
        {
            ViewModel.StartRecordingCommand.Execute(null);
            // Keep focus on the page so KeyDown events are captured.
            Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }

    // ── Key capture during recording ─────────────────────────────────

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.IsRecording)
            return;

        // Escape cancels recording without changing the binding.
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.StopRecordingCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ignore standalone modifier presses — wait for the primary key.
        if (IsModifierKey(e.Key))
            return;

        var keyName = MapVirtualKey(e.Key);
        if (keyName is null)
            return;

        var modifiers = GetCurrentModifiers();
        var binding = new HotkeyBinding(modifiers, keyName);
        ViewModel.ApplyRecordedBinding(binding);
        e.Handled = true;
    }

    private void OnPageLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.IsRecording)
            ViewModel.StopRecordingCommand.Execute(null);
    }

    // ── Modifier / key helpers ───────────────────────────────────────

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;

        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyModifiers.Ctrl;

        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyModifiers.Alt;

        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyModifiers.Shift;

        state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
        var stateR = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);
        if (state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)
            || stateR.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            modifiers |= HotkeyModifiers.Meta;

        return modifiers;
    }

    private static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static string? MapVirtualKey(VirtualKey key) => key switch
    {
        VirtualKey.Space => "Space",
        VirtualKey.Enter => "Enter",
        VirtualKey.Tab => "Tab",
        VirtualKey.Escape => "Escape",
        VirtualKey.Back => "Backspace",
        VirtualKey.Delete => "Delete",
        VirtualKey.Insert => "Insert",
        VirtualKey.Home => "Home",
        VirtualKey.End => "End",
        VirtualKey.PageUp => "PageUp",
        VirtualKey.PageDown => "PageDown",
        VirtualKey.Up => "Up",
        VirtualKey.Down => "Down",
        VirtualKey.Left => "Left",
        VirtualKey.Right => "Right",
        VirtualKey.Snapshot => "PrintScreen",
        VirtualKey.Pause => "Pause",

        >= VirtualKey.F1 and <= VirtualKey.F12 => $"F{(int)key - (int)VirtualKey.F1 + 1}",
        >= VirtualKey.A and <= VirtualKey.Z => ((char)('A' + key - VirtualKey.A)).ToString(),
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)('0' + key - VirtualKey.Number0)).ToString(),

        (VirtualKey)189 => "Minus",    // VK_OEM_MINUS
        (VirtualKey)187 => "Equals",   // VK_OEM_PLUS (=/+ key)
        (VirtualKey)192 => "BackQuote", // VK_OEM_3 (`/~ key)
        (VirtualKey)188 => "Comma",    // VK_OEM_COMMA
        (VirtualKey)190 => "Period",   // VK_OEM_PERIOD
        (VirtualKey)191 => "Slash",    // VK_OEM_2 (/?  key)
        (VirtualKey)186 => "Semicolon", // VK_OEM_1 (;/: key)
        (VirtualKey)222 => "Quote",    // VK_OEM_7 ('/\" key)
        (VirtualKey)220 => "Backslash", // VK_OEM_5 (\/| key)

        _ => null
    };
}
