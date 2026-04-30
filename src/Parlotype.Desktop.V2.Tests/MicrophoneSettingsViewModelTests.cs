using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

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
