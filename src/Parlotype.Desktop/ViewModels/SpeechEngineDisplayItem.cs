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

    /// <summary>Localized (ADR-064) — <see cref="Settings.SpeechEngineSettingsViewModel.OnCultureChanged"/> refreshes it.</summary>
    [ObservableProperty]
    private string _displayName = displayName;

    /// <summary>Localized (ADR-064) — see <see cref="DisplayName"/>.</summary>
    [ObservableProperty]
    private string _description = description;

    public ICommand SelectCommand { get; } = selectCommand;

    /// <summary>Onboarding highlight id for this card (ADR-056).</summary>
    public string OnboardingId { get; } = OnboardingTargetIds.EngineCard(type);

    [ObservableProperty]
    private bool _isSelected;
}
