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
    public void Section_IsRestrictedToParakeetEngine()
    {
        var settings = new MockSettingsService();
        var vm = new ParakeetModelSettingsViewModel(settings);

        Assert.Equal(SpeechEngine.Parakeet, vm.RestrictToEngine);
        Assert.Equal(SettingsCategory.SpeechEngine, vm.Category);
    }
}
