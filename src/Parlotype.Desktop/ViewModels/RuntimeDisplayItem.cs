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

    /// <summary>Localized (ADR-064) — <see cref="Settings.RuntimeSettingsViewModel.OnCultureChanged"/> refreshes it.</summary>
    [ObservableProperty]
    private string _displayName = displayName;

    /// <summary>Localized (ADR-064) — see <see cref="DisplayName"/>.</summary>
    [ObservableProperty]
    private string _description = description;

    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private string? _unavailableReason;
}
