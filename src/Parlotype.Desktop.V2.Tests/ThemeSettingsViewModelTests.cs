using Parlotype.Core.Settings;
using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class ThemeSettingsViewModelTests
{
    [Fact]
    public void SelectTheme_RaisesThemeChanged_AndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new ThemeSettingsViewModel(settings);

        AppTheme? raised = null;
        vm.ThemeChanged += (_, t) => raised = t;

        vm.SelectThemeCommand.Execute(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, vm.SelectedTheme);
        Assert.Equal(AppTheme.Dark, raised);
    }
}
