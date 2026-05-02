using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.V2.ViewModels;

public sealed partial class WaitTimeDisplayItem(WaitTimeOption option, ICommand selectCommand)
    : ObservableObject
{
    public WaitTimeOption Option { get; } = option;
    public string DisplayName { get; } = $"{option.GetDisplayName()} ({option.GetSeconds():G}s)";
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;
}
