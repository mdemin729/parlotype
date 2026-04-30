using Parlotype.Core.Speech;
using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class WhisperModelSettingsViewModelTests
{
    [Fact]
    public void SelectModel_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperModelSettingsViewModel(settings);

        vm.SelectModelCommand.Execute(WhisperModelType.Small);

        Assert.Equal(WhisperModelType.Small, vm.SelectedModel);
        Assert.True(vm.ModelOptions.First(m => m.Type == WhisperModelType.Small).IsSelected);
        Assert.False(vm.ModelOptions.First(m => m.Type == WhisperModelType.Base).IsSelected);
    }
}
