using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SpeechEngineSettingsViewModelTests
{
    [Fact]
    public void EngineOptions_ContainsTwoEntries()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        Assert.Equal(2, vm.EngineOptions.Length);
        Assert.Equal("Whisper", vm.EngineOptions[0].DisplayName);
        Assert.Equal("Gemma 4 (Experimental)", vm.EngineOptions[1].DisplayName);
    }

    [Fact]
    public void DefaultEngine_IsWhisper()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        Assert.Equal(SpeechEngine.Whisper, vm.SelectedEngine);
        Assert.False(vm.IsGemma4Selected);
    }

    [Fact]
    public void SelectEngine_Gemma4_UpdatesSelection()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.Equal(SpeechEngine.Gemma4, vm.SelectedEngine);
        Assert.True(vm.IsGemma4Selected);
        Assert.True(vm.EngineOptions[1].IsSelected);
        Assert.False(vm.EngineOptions[0].IsSelected);
    }

    [Fact]
    public void SelectEngine_BackToWhisper_UpdatesSelection()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);
        vm.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        Assert.Equal(SpeechEngine.Whisper, vm.SelectedEngine);
        Assert.False(vm.IsGemma4Selected);
        Assert.True(vm.EngineOptions[0].IsSelected);
        Assert.False(vm.EngineOptions[1].IsSelected);
    }

    [Fact]
    public async Task SelectEngine_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        // Give the async fire-and-forget a moment to complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("SpeechEngine", TestContext.Current.CancellationToken);
        Assert.Equal("Gemma4", saved);
    }
}
