using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WhisperOutputSettingsViewModelTests
{
    [Fact]
    public async Task Init_DefaultsPunctuationOn_ProfanityOff()
    {
        var settings = new MockSettingsService();

        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        Assert.True(vm.AutomaticPunctuationEnabled);
        Assert.False(vm.FilterProfanityEnabled);
    }

    [Fact]
    public async Task Init_LoadsSavedToggles()
    {
        var settings = new MockSettingsService();
        var ct = TestContext.Current.CancellationToken;
        await settings.SetAsync(SettingsKeys.AutomaticPunctuation, false.ToString(), ct);
        await settings.SetAsync(SettingsKeys.FilterProfanity, true.ToString(), ct);

        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        Assert.False(vm.AutomaticPunctuationEnabled);
        Assert.True(vm.FilterProfanityEnabled);
    }

    [Fact]
    public async Task TogglingPunctuation_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        vm.AutomaticPunctuationEnabled = false;
        await Task.Yield();

        Assert.Equal(false.ToString(),
            await settings.GetAsync<string>(SettingsKeys.AutomaticPunctuation, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TogglingProfanity_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        vm.FilterProfanityEnabled = true;
        await Task.Yield();

        Assert.Equal(true.ToString(),
            await settings.GetAsync<string>(SettingsKeys.FilterProfanity, TestContext.Current.CancellationToken));
    }
}
