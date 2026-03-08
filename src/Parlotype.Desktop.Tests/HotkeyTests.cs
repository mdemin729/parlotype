using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class HotkeyRecorderViewTests
{
    [AvaloniaFact]
    public void HotkeyRecorderView_Renders_With_Default_Binding()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);

        var view = new HotkeyRecorderView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();

        Assert.Equal("Ctrl+Shift+Space", vm.DisplayText);
        Assert.False(vm.IsRecording);

        window.Close();
    }

    [AvaloniaFact]
    public void StartRecording_Changes_DisplayText()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);

        vm.StartRecordingCommand.Execute(null);

        Assert.True(vm.IsRecording);
        Assert.Equal("Press keys...", vm.DisplayText);
    }

    [AvaloniaFact]
    public void StopRecording_Restores_DisplayText()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);

        vm.StartRecordingCommand.Execute(null);
        vm.StopRecordingCommand.Execute(null);

        Assert.False(vm.IsRecording);
        Assert.Equal("Ctrl+Shift+Space", vm.DisplayText);
    }

    [AvaloniaFact]
    public void ApplyRecordedBinding_Updates_CurrentBinding()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(hotkeyService, settings);

        vm.StartRecordingCommand.Execute(null);

        var newBinding = new HotkeyBinding(HotkeyModifiers.Alt, "Space");
        vm.ApplyRecordedBinding(newBinding);

        Assert.False(vm.IsRecording);
        Assert.Equal(newBinding, vm.CurrentBinding);
        Assert.Equal("Alt+Space", vm.DisplayText);
        Assert.Equal(newBinding, hotkeyService.CurrentBinding);
    }

    [AvaloniaFact]
    public void ApplyRecordedBinding_Rejects_Invalid_Binding()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);
        var originalBinding = vm.CurrentBinding;

        vm.StartRecordingCommand.Execute(null);

        // No modifier → invalid
        var invalidBinding = new HotkeyBinding(HotkeyModifiers.None, "Space");
        vm.ApplyRecordedBinding(invalidBinding);

        Assert.False(vm.IsRecording);
        Assert.Equal(originalBinding, vm.CurrentBinding);
    }

    [AvaloniaFact]
    public void ConflictWarning_Shows_For_Reserved_Shortcut()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(hotkeyService, settings);

        vm.StartRecordingCommand.Execute(null);

        // Win+L is reserved
        var reserved = new HotkeyBinding(HotkeyModifiers.Meta, "L");
        vm.ApplyRecordedBinding(reserved);

        Assert.NotNull(vm.ConflictWarning);
        Assert.Contains("Lock workstation", vm.ConflictWarning!);
    }

    [AvaloniaFact]
    public void ConflictWarning_Null_For_Safe_Binding()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);

        // Default is Ctrl+Shift+Space — not reserved
        Assert.Null(vm.ConflictWarning);
    }

    [AvaloniaFact]
    public void ActivationMode_Toggles_Between_PTT_And_Toggle()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(hotkeyService, settings);

        Assert.True(vm.IsPushToTalk);
        Assert.False(vm.IsToggle);

        vm.IsToggle = true;

        Assert.True(vm.IsToggle);
        Assert.False(vm.IsPushToTalk);
        Assert.Equal(ActivationMode.Toggle, vm.CurrentMode);
        Assert.Equal(ActivationMode.Toggle, hotkeyService.Mode);
    }

    [AvaloniaFact]
    public async Task ApplyRecordedBinding_Persists_To_Settings()
    {
        var settings = new MockSettingsService();
        var vm = new HotkeyRecorderViewModel(null, settings);

        var newBinding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "F1");
        vm.ApplyRecordedBinding(newBinding);

        // Allow fire-and-forget persistence to complete
        await Task.Delay(50);

        var savedMods = await settings.GetAsync<string>("HotkeyModifiers");
        var savedKey = await settings.GetAsync<string>("HotkeyKey");

        Assert.Equal("Ctrl, Alt", savedMods);
        Assert.Equal("F1", savedKey);
    }
}

public class MainWindowHotkeyTests
{
    private static readonly Core.Audio.MicrophoneInfo Mic1 = new("mic-1", "Test Mic", true);

    [AvaloniaFact]
    public async Task HotkeyService_Initializes_On_Startup()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var settingsVm = new SettingsViewModel(
            new MockMicrophoneEnumerator(Mic1), settings, hotkeyService: hotkeyService);
        var mainVm = new MainWindowViewModel(settingsVm, hotkeyService: hotkeyService);

        await mainVm.InitializeHotkeyServiceAsync();

        Assert.True(hotkeyService.IsStarted);
    }

    [AvaloniaFact]
    public async Task HotkeyPress_Does_Not_Crash_Without_Pipeline()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var settingsVm = new SettingsViewModel(
            new MockMicrophoneEnumerator(Mic1), settings, hotkeyService: hotkeyService);
        var mainVm = new MainWindowViewModel(settingsVm, hotkeyService: hotkeyService);

        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        await mainVm.InitializeHotkeyServiceAsync();

        // Simulate hotkey press without a pipeline — should log warning but not crash
        hotkeyService.SimulatePress();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        // IsRecording stays false because pipeline is null
        Assert.False(mainVm.IsRecording);

        window.Close();
    }

    [AvaloniaFact]
    public void Dispose_Stops_Hotkey_Service()
    {
        var hotkeyService = new MockGlobalHotkeyService();
        var settings = new MockSettingsService();
        var settingsVm = new SettingsViewModel(
            new MockMicrophoneEnumerator(Mic1), settings, hotkeyService: hotkeyService);
        var mainVm = new MainWindowViewModel(settingsVm, hotkeyService: hotkeyService);

        hotkeyService.IsStarted = true;
        mainVm.Dispose();

        Assert.False(hotkeyService.IsStarted);
    }
}
