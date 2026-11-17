using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class MicrophoneSettingsViewModelTests
{
    [Fact]
    public async Task Initialize_SelectsFirstDevice_WhenNoSavedId()
    {
        var settings = new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(
            new MicrophoneInfo("a", "Mic A", true),
            new MicrophoneInfo("b", "Mic B", false));

        var vm = new MicrophoneSettingsViewModel(enumerator, settings);

        // Wait for the async InitializeAsync started in the ctor.
        await WaitFor(() => vm.SelectedMicrophone is not null);

        Assert.Equal("a", vm.SelectedMicrophone!.Id);
        Assert.Equal(2, vm.AvailableMicrophones.Count);
    }

    [Fact]
    public async Task Initialize_RestoresPersistedSelection()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedMicrophoneId, "b", TestContext.Current.CancellationToken);
        var enumerator = new MockMicrophoneEnumerator(
            new MicrophoneInfo("a", "Mic A", true),
            new MicrophoneInfo("b", "Mic B", false));

        var vm = new MicrophoneSettingsViewModel(enumerator, settings);

        await WaitFor(() => vm.SelectedMicrophone?.Id == "b");

        Assert.Equal("b", vm.SelectedMicrophone!.Id);
    }

    /// <summary>
    /// macOS reports no devices until CoreAudio is up, which can be after the
    /// user answers the microphone prompt (ADR-077). When the list then arrives,
    /// the saved microphone has to win over "the first one in the list".
    /// </summary>
    /// <remarks>
    /// This test and the next never really wait. The mocks return completed
    /// tasks and the view model posts its refresh to the dispatcher, so
    /// draining the dispatcher is enough, and the test stays on the dispatcher
    /// thread from start to finish. A real wait here failed on the Windows
    /// runner only (docs/knowledge/culture-changing-tests-need-avaloniafact.md).
    /// </remarks>
    [AvaloniaFact]
    public async Task DevicesArrivingAfterAnEmptyStart_RestoreTheSavedMicrophone()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedMicrophoneId, "b", ct);
        var enumerator = new MockMicrophoneEnumerator();

        var vm = new MicrophoneSettingsViewModel(enumerator, settings);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(vm.SelectedMicrophone);

        enumerator.SetDevices(
            new MicrophoneInfo("a", "Mic A", true),
            new MicrophoneInfo("b", "Mic B", false));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("b", vm.SelectedMicrophone?.Id);
        Assert.Equal("b", await settings.GetAsync<string>(SettingsKeys.SelectedMicrophoneId, ct));
    }

    [AvaloniaFact]
    public void ADeviceChange_KeepsTheCurrentSelection()
    {
        var settings = new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(
            new MicrophoneInfo("a", "Mic A", true),
            new MicrophoneInfo("b", "Mic B", false));
        var vm = new MicrophoneSettingsViewModel(enumerator, settings);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a", vm.SelectedMicrophone?.Id);
        vm.SelectMicrophoneCommand.Execute(vm.AvailableMicrophones[1]);

        enumerator.SetDevices(
            new MicrophoneInfo("c", "Mic C", true),
            new MicrophoneInfo("b", "Mic B", false));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(vm.AvailableMicrophones, m => m.Info.Id == "c");
        Assert.Equal("b", vm.SelectedMicrophone?.Id);
    }

    private static async Task WaitFor(Func<bool> predicate, int timeoutMs = 1000)
    {
        var elapsed = 0;
        var ct = TestContext.Current.CancellationToken;
        while (!predicate() && elapsed < timeoutMs)
        {
            await Task.Delay(20, ct);
            elapsed += 20;
        }
    }
}
