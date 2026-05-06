using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class RuntimeDisplayItem(
    RuntimePreference type,
    string displayName,
    string description,
    ICommand selectCommand)
    : ObservableObject
{
    public RuntimePreference Type { get; } = type;
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private string? _unavailableReason;
}
