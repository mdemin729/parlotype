using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class Gemma4ModelDisplayItem(
    Gemma4ModelInfo info,
    bool isInstalled,
    ICommand selectCommand,
    ICommand downloadCommand,
    ICommand deleteCommand)
    : ObservableObject
{
    public string ModelId { get; } = info.ModelId;
    public string DisplayName { get; } = info.DisplayName;
    public string DiskSize { get; } = info.DiskSize;
    public ICommand SelectCommand { get; } = selectCommand;
    public ICommand DownloadCommand { get; } = downloadCommand;
    public ICommand DeleteCommand { get; } = deleteCommand;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInstalled = isInstalled;
}
