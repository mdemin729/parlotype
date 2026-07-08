using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SpeechEngineSettingsViewModelTests
{
    [Fact]
    public void EngineOptions_ContainsThreeEntries_ParakeetFirst()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        Assert.Equal(3, vm.EngineOptions.Length);
        Assert.Equal("Parakeet v3 (Recommended)", vm.EngineOptions[0].DisplayName);
        Assert.Equal("Whisper", vm.EngineOptions[1].DisplayName);
        Assert.Equal("Gemma 4 (Experimental)", vm.EngineOptions[2].DisplayName);
    }

    [Fact]
    public void DefaultEngine_IsParakeet()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        Assert.Equal(SpeechEngine.Parakeet, vm.SelectedEngine);
        Assert.True(vm.IsParakeetSelected);
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
        Assert.False(vm.IsParakeetSelected);
        Assert.True(vm.EngineOptions[2].IsSelected);
        Assert.False(vm.EngineOptions[0].IsSelected);
    }

    [Fact]
    public void SelectEngine_Whisper_UpdatesSelection()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);
        vm.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        Assert.Equal(SpeechEngine.Whisper, vm.SelectedEngine);
        Assert.False(vm.IsGemma4Selected);
        Assert.False(vm.IsParakeetSelected);
        Assert.True(vm.EngineOptions[1].IsSelected);
        Assert.False(vm.EngineOptions[0].IsSelected);
    }

    [Fact]
    public void SelectEngine_BackToParakeet_UpdatesSelection()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Whisper);
        vm.SelectEngineCommand.Execute(SpeechEngine.Parakeet);

        Assert.Equal(SpeechEngine.Parakeet, vm.SelectedEngine);
        Assert.True(vm.IsParakeetSelected);
        Assert.False(vm.IsGemma4Selected);
        Assert.True(vm.EngineOptions[0].IsSelected);
        Assert.False(vm.EngineOptions[1].IsSelected);
    }

    [Fact]
    public async Task SelectEngine_Parakeet_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.Whisper);
        vm.SelectEngineCommand.Execute(SpeechEngine.Parakeet);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("SpeechEngine", TestContext.Current.CancellationToken);
        Assert.Equal("Parakeet", saved);
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

    [Fact]
    public void Prewarm_DefaultsToFalse_WhenUnset()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        Assert.False(vm.PreloadModelOnStartupEnabled);
    }

    [Fact]
    public async Task Prewarm_LoadsPersistedTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.PrewarmModelOnStartup, true.ToString(), ct);

        var vm = new SpeechEngineSettingsViewModel(settings);

        // InitializeAsync is fire-and-forget — give it a moment to load.
        await Task.Delay(100, ct);

        Assert.True(vm.PreloadModelOnStartupEnabled);
    }

    [Fact]
    public async Task Prewarm_TogglePersistsSetting()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.PreloadModelOnStartupEnabled = true;
        await Task.Delay(50, ct);
        Assert.Equal(true.ToString(),
            await settings.GetAsync<string>(SettingsKeys.PrewarmModelOnStartup, ct));

        vm.PreloadModelOnStartupEnabled = false;
        await Task.Delay(50, ct);
        Assert.Equal(false.ToString(),
            await settings.GetAsync<string>(SettingsKeys.PrewarmModelOnStartup, ct));
    }
}
