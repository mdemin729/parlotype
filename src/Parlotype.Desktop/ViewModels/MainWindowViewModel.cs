using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Ready";
}
