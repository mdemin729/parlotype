using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Onboarding;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class SpeechEngineDisplayItem(
    SpeechEngine type,
    string displayName,
    string description,
    ICommand selectCommand)
    : ObservableObject
{
    public SpeechEngine Type { get; } = type;
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
    public ICommand SelectCommand { get; } = selectCommand;

    /// <summary>Onboarding highlight id for this card (ADR-056).</summary>
    public string OnboardingId { get; } = OnboardingTargetIds.EngineCard(type);

    [ObservableProperty]
    private bool _isSelected;
}
