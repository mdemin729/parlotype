using Avalonia.Headless.XUnit;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class HotkeySettingsViewModelTests
{
    private static async Task<(HotkeySettingsViewModel Vm, MockGlobalHotkeyService Hotkey, MockSettingsService Settings)>
        CreateAsync()
    {
        var hotkey = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var vm = new HotkeySettingsViewModel(hotkey, settings);
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (vm, hotkey, settings);
    }

    [AvaloniaFact]
    public async Task Loads_The_Default_Bindings()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.Equal(3, vm.Bindings.Count);
        Assert.Equal("Hold Right Ctrl", vm.Bindings[0].DisplayString);
        Assert.Equal("Push to talk", vm.Bindings[0].ModeLabel);
        Assert.True(vm.HasBindings);
    }

    [AvaloniaFact]
    public async Task Only_Chord_Rows_Can_Change_Mode()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.False(vm.Bindings[0].CanChangeMode); // hold
        Assert.False(vm.Bindings[1].CanChangeMode); // double-tap
        Assert.True(vm.Bindings[2].CanChangeMode);  // chord
    }

    [AvaloniaFact]
    public async Task Adding_A_Preset_Appends_It_And_Persists()
    {
        var (vm, hotkey, _) = await CreateAsync();

        vm.AddPresetCommand.Execute(DictationHotkey.Hold(ModifierKey.Alt, ModifierSide.Right));

        Assert.Equal(4, vm.Bindings.Count);
        Assert.Equal("Hold Right Alt", vm.Bindings[3].DisplayString);
        Assert.Equal(4, hotkey.Bindings.Count);
    }

    [AvaloniaFact]
    public async Task Removing_A_Binding_Persists_The_Rest()
    {
        var (vm, hotkey, _) = await CreateAsync();

        vm.RemoveBindingCommand.Execute(vm.Bindings[0]);

        Assert.Equal(2, vm.Bindings.Count);
        Assert.Equal(2, hotkey.Bindings.Count);
        Assert.DoesNotContain(hotkey.Bindings, b => b.DisplayString == "Hold Right Ctrl");
    }

    [AvaloniaFact]
    public async Task Removing_Every_Binding_Clears_HasBindings()
    {
        var (vm, _, _) = await CreateAsync();

        while (vm.Bindings.Count > 0)
            vm.RemoveBindingCommand.Execute(vm.Bindings[0]);

        Assert.False(vm.HasBindings);
    }

    [AvaloniaFact]
    public async Task Toggling_A_Chord_Mode_Flips_It()
    {
        var (vm, hotkey, _) = await CreateAsync();

        vm.ToggleBindingModeCommand.Execute(vm.Bindings[2]);

        Assert.Equal("Push to talk", vm.Bindings[2].ModeLabel);
        Assert.Equal(ActivationMode.PushToTalk, hotkey.Bindings[2].Mode);
    }

    [AvaloniaFact]
    public async Task Toggling_A_Hold_Mode_Does_Nothing()
    {
        // A hold has to be push-to-talk; releasing the key must mean "stop".
        var (vm, _, _) = await CreateAsync();

        vm.ToggleBindingModeCommand.Execute(vm.Bindings[0]);

        Assert.Equal("Push to talk", vm.Bindings[0].ModeLabel);
    }

    [AvaloniaFact]
    public async Task Reserved_Chord_Is_Rejected_With_A_Blocking_Message()
    {
        var (vm, _, _) = await CreateAsync();

        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Meta, "L"));

        Assert.Equal(3, vm.Bindings.Count);
        Assert.NotNull(vm.BlockingWarning);
        Assert.Contains("Lock workstation", vm.BlockingWarning);
        Assert.Null(vm.AdvisoryWarning);
    }

    [AvaloniaFact]
    public async Task Duplicate_Chord_Is_Rejected()
    {
        var (vm, _, _) = await CreateAsync();

        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"));

        Assert.Equal(3, vm.Bindings.Count);
        Assert.Contains("already bound", vm.BlockingWarning);
    }

    [AvaloniaFact]
    public async Task Colliding_Chord_Is_Accepted_With_An_Advisory()
    {
        var (vm, _, _) = await CreateAsync();

        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space"));

        Assert.Equal(4, vm.Bindings.Count);
        Assert.Null(vm.BlockingWarning);
        Assert.Contains("Visual Studio", vm.AdvisoryWarning);
    }

    [AvaloniaFact]
    public async Task Recording_State_Drives_The_Recorder_Label()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.False(vm.IsRecording);
        Assert.Equal("Record a chord…", vm.RecorderText);

        vm.StartRecordingCommand.Execute(null);
        Assert.True(vm.IsRecording);
        Assert.Equal("Press a key combination…", vm.RecorderText);

        vm.StopRecordingCommand.Execute(null);
        Assert.False(vm.IsRecording);
        Assert.Equal("Record a chord…", vm.RecorderText);
    }

    [AvaloniaFact]
    public async Task Applying_A_Chord_Leaves_Recording_State()
    {
        var (vm, _, _) = await CreateAsync();
        vm.StartRecordingCommand.Execute(null);

        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Ctrl, "F9"));

        Assert.False(vm.IsRecording);
        Assert.Equal(4, vm.Bindings.Count);
    }

    [AvaloniaFact]
    public async Task Without_A_Hotkey_Service_Changes_Are_Persisted_Directly()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeySettingsViewModel(hotkeyService: null, settings);
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        vm.RemoveBindingCommand.Execute(vm.Bindings[0]);
        await Task.Delay(50);

        var stored = await settings.GetAsync<List<string>>(SettingsKeys.HotkeyBindings);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Count);
    }

    [AvaloniaFact]
    public async Task Refresh_Picks_Up_Bindings_Changed_Elsewhere()
    {
        var (vm, hotkey, _) = await CreateAsync();

        hotkey.UpdateBindings([DictationHotkeyDefaults.ChordFallback]);
        vm.Refresh();

        var only = Assert.Single(vm.Bindings);
        Assert.Equal("Ctrl+Alt+Space", only.DisplayString);
    }
}
