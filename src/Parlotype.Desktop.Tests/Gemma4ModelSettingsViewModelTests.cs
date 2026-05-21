using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class Gemma4ModelSettingsViewModelTests
{
    [Fact]
    public void ModelOptions_ContainsAllCatalogEntries()
    {
        var settings = new MockSettingsService();
        var vm = new Gemma4ModelSettingsViewModel(settings);

        Assert.Equal(Gemma4ModelInfo.All.Count, vm.ModelOptions.Length);
        Assert.Equal(
            Gemma4ModelInfo.All.Select(m => m.ModelId),
            vm.ModelOptions.Select(o => o.ModelId));
    }

    [Fact]
    public void DefaultSelectedModel_IsCatalogDefault()
    {
        var settings = new MockSettingsService();
        var vm = new Gemma4ModelSettingsViewModel(settings);

        Assert.Equal(Gemma4ModelInfo.Default.ModelId, vm.SelectedModelId);
        Assert.True(vm.ModelOptions.Single(m => m.ModelId == Gemma4ModelInfo.Default.ModelId).IsSelected);
    }

    [Fact]
    public async Task SavedModel_IsHonoredOnInit()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedGemma4Model, Gemma4ModelInfo.E2B_Q8_0.ModelId,
            TestContext.Current.CancellationToken);

        var vm = new Gemma4ModelSettingsViewModel(settings);
        await Task.Yield();

        Assert.Equal(Gemma4ModelInfo.E2B_Q8_0.ModelId, vm.SelectedModelId);
        Assert.True(vm.ModelOptions.Single(m => m.ModelId == Gemma4ModelInfo.E2B_Q8_0.ModelId).IsSelected);
    }

    [Fact]
    public async Task SelectModel_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new Gemma4ModelSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectModelCommand.Execute(Gemma4ModelInfo.E4B_BF16.ModelId);
        await Task.Yield();

        Assert.Equal(Gemma4ModelInfo.E4B_BF16.ModelId, vm.SelectedModelId);
        Assert.True(vm.ModelOptions.Single(m => m.ModelId == Gemma4ModelInfo.E4B_BF16.ModelId).IsSelected);
        Assert.All(vm.ModelOptions.Where(m => m.ModelId != Gemma4ModelInfo.E4B_BF16.ModelId),
            m => Assert.False(m.IsSelected));

        var persisted = await settings.GetAsync<string>(SettingsKeys.SelectedGemma4Model,
            TestContext.Current.CancellationToken);
        Assert.Equal(Gemma4ModelInfo.E4B_BF16.ModelId, persisted);
    }

    [Fact]
    public async Task SelectModel_UnloadsRecognizerWhenReady()
    {
        var settings = new MockSettingsService();
        var recognizer = new MockSpeechRecognizer();
        await recognizer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(recognizer.IsReady);

        var vm = new Gemma4ModelSettingsViewModel(settings, recognizer: recognizer);
        await Task.Yield();

        vm.SelectModelCommand.Execute(Gemma4ModelInfo.E2B_Q8_0.ModelId);
        await Task.Yield();

        Assert.False(recognizer.IsReady);
        Assert.Equal(1, recognizer.UnloadCount);
    }

    [Fact]
    public void Section_IsRestrictedToGemma4Engine()
    {
        var settings = new MockSettingsService();
        var vm = new Gemma4ModelSettingsViewModel(settings);

        Assert.Equal(SpeechEngine.Gemma4, vm.RestrictToEngine);
        Assert.Equal(SettingsCategory.SpeechEngine, vm.Category);
    }
}
