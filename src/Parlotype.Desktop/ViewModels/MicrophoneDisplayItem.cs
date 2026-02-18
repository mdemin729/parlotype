using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Audio;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class MicrophoneDisplayItem(MicrophoneInfo info, ICommand selectCommand)
    : ObservableObject
{
    public MicrophoneInfo Info { get; } = info;
    public string Name { get; } = info.Name;
    public bool IsSystemDefault { get; } = info.IsDefault;
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private double _itemOpacity = 1.0;
}
