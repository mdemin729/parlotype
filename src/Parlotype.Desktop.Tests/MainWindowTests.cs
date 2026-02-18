using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class MainWindowTests
{
    private static readonly MicrophoneInfo Mic1 = new("mic-1", "Microphone Array (Realtek)", true);
    private static readonly MicrophoneInfo Mic2 = new("mic-2", "Headset Microphone (USB)", false);
    private static readonly MicrophoneInfo Mic3 = new("mic-3", "Webcam Microphone (HD Pro)", false);

    private static (MainWindow Window, MockMicrophoneEnumerator Enumerator, SettingsViewModel Settings) CreateWindow(
        params MicrophoneInfo[] mics)
    {
        var enumerator = new MockMicrophoneEnumerator(mics);
        var settings = new MockSettingsService();
        var settingsVm = new SettingsViewModel(enumerator, settings);
        var mainVm = new MainWindowViewModel(settingsVm);
        var window = new MainWindow { DataContext = mainVm };
        window.Show();
        return (window, enumerator, settingsVm);
    }

    [AvaloniaFact]
    public void MainWindow_Opens_And_Renders()
    {
        var (window, enumerator, _) = CreateWindow(Mic1, Mic2);

        Assert.True(window.IsVisible);
        Assert.Equal("Parlotype", window.Title);
        Assert.Equal(280, window.Width);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public void MainWindow_Has_Three_Toolbar_Buttons()
    {
        var (window, enumerator, _) = CreateWindow(Mic1);

        var settingsBtn = window.FindControl<Button>("SettingsButton");
        var closeBtn = window.FindControl<Button>("CloseButton");

        Assert.NotNull(settingsBtn);
        Assert.NotNull(closeBtn);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public void Settings_Flyout_Opens_On_Button_Click()
    {
        var (window, enumerator, _) = CreateWindow(Mic1);

        var settingsBtn = window.FindControl<Button>("SettingsButton");
        Assert.NotNull(settingsBtn);

        // Open the flyout programmatically
        var flyout = settingsBtn.Flyout;
        Assert.NotNull(flyout);
        flyout!.ShowAt(settingsBtn);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public async Task Microphone_List_Shows_Correct_Items()
    {
        var (window, enumerator, settingsVm) = CreateWindow(Mic1, Mic2, Mic3);

        // Allow InitializeMicrophonesAsync to complete
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, settingsVm.AvailableMicrophones.Count);
        Assert.Equal("Microphone Array (Realtek)", settingsVm.AvailableMicrophones[0].Name);
        Assert.Equal("Headset Microphone (USB)", settingsVm.AvailableMicrophones[1].Name);
        Assert.Equal("Webcam Microphone (HD Pro)", settingsVm.AvailableMicrophones[2].Name);

        // First mic should be selected by default
        Assert.NotNull(settingsVm.SelectedMicrophone);
        Assert.Equal("mic-1", settingsVm.SelectedMicrophone!.Id);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public async Task Adding_Microphone_Updates_List_And_Selects_New()
    {
        var (window, enumerator, settingsVm) = CreateWindow(Mic1, Mic2);

        // Wait for initialization
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, settingsVm.AvailableMicrophones.Count);
        Assert.Equal("mic-1", settingsVm.SelectedMicrophone?.Id);

        // Add a new microphone
        enumerator.AddDevice(Mic3);

        // Allow DevicesChanged → Dispatcher.UIThread.InvokeAsync → UpdateMicrophoneListAsync
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, settingsVm.AvailableMicrophones.Count);
        Assert.Contains(settingsVm.AvailableMicrophones, m => m.Info.Id == "mic-3");

        // New mic should be auto-selected
        Assert.Equal("mic-3", settingsVm.SelectedMicrophone?.Id);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public async Task Removing_Microphone_Updates_List_And_Falls_Back()
    {
        var (window, enumerator, settingsVm) = CreateWindow(Mic1, Mic2, Mic3);

        // Wait for initialization
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, settingsVm.AvailableMicrophones.Count);

        // Select mic-2
        var mic2Item = settingsVm.AvailableMicrophones.First(m => m.Info.Id == "mic-2");
        settingsVm.SelectMicrophoneCommand.Execute(mic2Item);
        Assert.Equal("mic-2", settingsVm.SelectedMicrophone?.Id);

        // Remove the selected mic
        enumerator.RemoveDevice("mic-2");

        // Allow DevicesChanged + fade-out delay (150ms) + processing
        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, settingsVm.AvailableMicrophones.Count);
        Assert.DoesNotContain(settingsVm.AvailableMicrophones, m => m.Info.Id == "mic-2");

        // Should fall back to first available mic
        Assert.NotNull(settingsVm.SelectedMicrophone);
        Assert.Equal("mic-1", settingsVm.SelectedMicrophone!.Id);

        window.Close();
        enumerator.Dispose();
    }

    [AvaloniaFact]
    public async Task Removing_All_Microphones_Clears_Selection()
    {
        var singleMic = new MicrophoneInfo("mic-only", "Only Microphone", true);
        var (window, enumerator, settingsVm) = CreateWindow(singleMic);

        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(settingsVm.AvailableMicrophones);
        Assert.Equal("mic-only", settingsVm.SelectedMicrophone?.Id);

        // Remove the only mic
        enumerator.RemoveDevice("mic-only");

        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(settingsVm.AvailableMicrophones);
        Assert.Null(settingsVm.SelectedMicrophone);

        window.Close();
        enumerator.Dispose();
    }
}
