using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SpeechEngineSettingsViewModelTests
{
    [Fact]
    public void EngineOptions_ContainsFiveEntries_LocalEnginesFirst()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        // Local engines stay listed first (Parakeet still "(Recommended)") —
        // the opt-in cloud engines (ADR-032) are appended at the end, never
        // nudged ahead of the local defaults.
        Assert.Equal(5, vm.EngineOptions.Length);
        Assert.Equal("Parakeet v3 (Recommended)", vm.EngineOptions[0].DisplayName);
        Assert.Equal("Whisper", vm.EngineOptions[1].DisplayName);
        Assert.Equal("Gemma 4 (Experimental)", vm.EngineOptions[2].DisplayName);
        Assert.Equal("OpenAI-compatible (Cloud)", vm.EngineOptions[3].DisplayName);
        Assert.Equal("xAI Grok (Cloud)", vm.EngineOptions[4].DisplayName);
    }

    [Fact]
    public void CloudEngineDescriptions_DiscloseAudioLeavesMachine()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        var openAi = vm.EngineOptions.Single(e => e.Type == SpeechEngine.OpenAiCompatible);
        var xai = vm.EngineOptions.Single(e => e.Type == SpeechEngine.XaiGrok);

        Assert.Contains("sent to the configured provider", openAi.Description);
        Assert.Contains("sent to xAI", xai.Description);
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
    public async Task SelectEngine_OpenAiCompatible_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.OpenAiCompatible);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("SpeechEngine", TestContext.Current.CancellationToken);
        Assert.Equal("OpenAiCompatible", saved);
        Assert.True(vm.IsOpenAiCompatSelected);
        Assert.False(vm.IsXaiGrokSelected);
    }

    [Fact]
    public async Task SelectEngine_XaiGrok_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);

        vm.SelectEngineCommand.Execute(SpeechEngine.XaiGrok);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("SpeechEngine", TestContext.Current.CancellationToken);
        Assert.Equal("XaiGrok", saved);
        Assert.True(vm.IsXaiGrokSelected);
        Assert.False(vm.IsOpenAiCompatSelected);
    }

    [AvaloniaFact]
    public void SelectEngine_CloudEngine_UpdatesTranscribeViewModelActiveEngine()
    {
        var settings = new MockSettingsService();
        var windowManager = new MockWindowManager();
        var transcribe = new TranscribeViewModel(windowManager);
        var vm = new SpeechEngineSettingsViewModel(settings, transcribe);

        Assert.False(transcribe.IsCloudEngineActive);

        vm.SelectEngineCommand.Execute(SpeechEngine.XaiGrok);

        Assert.True(transcribe.IsCloudEngineActive);
        Assert.Equal("Cloud: xAI Grok", transcribe.CloudProviderLabel);

        vm.SelectEngineCommand.Execute(SpeechEngine.Parakeet);

        Assert.False(transcribe.IsCloudEngineActive);
        Assert.Null(transcribe.CloudProviderLabel);
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
