using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class WhisperModelDisplayItem(WhisperModelInfo info, bool isCached, ICommand selectCommand)
    : ObservableObject
{
    public WhisperModelType Type { get; } = info.Type;
    public string DisplayName { get; } = info.DisplayName;
    public string DiskSize { get; } = info.DiskSize;
    public bool SupportsTranslation { get; } = info.SupportsTranslation;
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCached = isCached;
}
