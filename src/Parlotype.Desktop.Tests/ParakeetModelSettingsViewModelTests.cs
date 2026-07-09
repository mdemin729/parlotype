using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class ParakeetModelSettingsViewModelTests
{
    [Fact]
    public void ModelOptions_ContainsAllCatalogEntries()
    {
        var settings = new MockSettingsService();
        var vm = new ParakeetModelSettingsViewModel(settings);

        Assert.Equal(ParakeetModelInfo.All.Count, vm.ModelOptions.Length);
        Assert.Equal(
            ParakeetModelInfo.All.Select(m => m.ModelId),
            vm.ModelOptions.Select(o => o.ModelId));
    }

    [Fact]
    public void DefaultSelectedModel_IsCatalogDefault()
    {
        var settings = new MockSettingsService();
        var vm = new ParakeetModelSettingsViewModel(settings);

        Assert.Equal(ParakeetModelInfo.Default.ModelId, vm.SelectedModelId);
        Assert.True(vm.ModelOptions.Single(m => m.ModelId == ParakeetModelInfo.Default.ModelId).IsSelected);
    }

    [Fact]
    public async Task SavedModel_IsHonoredOnInit()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedParakeetModel, ParakeetModelInfo.TdtV3Int8.ModelId,
            TestContext.Current.CancellationToken);

        var vm = new ParakeetModelSettingsViewModel(settings);
        await Task.Yield();

        Assert.Equal(ParakeetModelInfo.TdtV3Int8.ModelId, vm.SelectedModelId);
    }

    [Fact]
    public async Task SelectModel_Fp32_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new ParakeetModelSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectModelCommand.Execute(ParakeetModelInfo.TdtV3Fp32.ModelId);
        await Task.Yield();

        Assert.Equal(ParakeetModelInfo.TdtV3Fp32.ModelId, vm.SelectedModelId);
        Assert.True(vm.ModelOptions.Single(m => m.ModelId == ParakeetModelInfo.TdtV3Fp32.ModelId).IsSelected);
        Assert.False(vm.ModelOptions.Single(m => m.ModelId == ParakeetModelInfo.TdtV3Int8.ModelId).IsSelected);

        var persisted = await settings.GetAsync<string>(SettingsKeys.SelectedParakeetModel,
            TestContext.Current.CancellationToken);
        Assert.Equal(ParakeetModelInfo.TdtV3Fp32.ModelId, persisted);
    }

    [Fact]
    public async Task SelectModel_UnloadsRecognizerWhenReady()
    {
        var settings = new MockSettingsService();
        var recognizer = new MockSpeechRecognizer();
        await recognizer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(recognizer.IsReady);

        var vm = new ParakeetModelSettingsViewModel(settings, recognizer: recognizer);
        await Task.Yield();

        vm.SelectModelCommand.Execute(ParakeetModelInfo.TdtV3Fp32.ModelId);
        await Task.Yield();

        Assert.False(recognizer.IsReady);
        Assert.Equal(1, recognizer.UnloadCount);
    }

    [Fact]
    public void Section_IsRestrictedToParakeetEngine()
    {
        var settings = new MockSettingsService();
        var vm = new ParakeetModelSettingsViewModel(settings);

        Assert.Equal(SpeechEngine.Parakeet, vm.RestrictToEngine);
        Assert.Equal(SettingsCategory.SpeechEngine, vm.Category);
    }
}
