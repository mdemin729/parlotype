using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Onboarding;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels.Onboarding;

/// <summary>
/// Drives the onboarding tour (ADR-055): holds the step list, the current
/// index, and Back/Next/Skip. On every step change it opens the step's target
/// window through <see cref="IWindowManager"/>. Highlighting and window
/// placement are view concerns (<c>OnboardingWindow</c>); persistence of the
/// shown-once flag is <c>OnboardingService</c>'s — this VM stays fully
/// headless-testable.
/// </summary>
public sealed partial class OnboardingWizardViewModel : ViewModelBase
{
    private readonly IWindowManager _windowManager;
    private readonly IGlobalHotkeyService? _hotkeyService;

    public OnboardingWizardViewModel(
        IWindowManager windowManager,
        IGlobalHotkeyService? hotkeyService = null)
    {
        _windowManager = windowManager;
        _hotkeyService = hotkeyService;
    }

    /// <summary>Raised when the tour is done (finished or skipped); the view closes itself.</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<OnboardingStepItemViewModel> Steps { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(IsFirstStep))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(NextButtonText))]
    [NotifyPropertyChangedFor(nameof(HasDetailLines))]
    private int _currentIndex;

    public OnboardingStep? CurrentStep =>
        CurrentIndex >= 0 && CurrentIndex < Steps.Count ? Steps[CurrentIndex].Step : null;

    public bool IsFirstStep => CurrentIndex == 0;

    public bool IsLastStep => Steps.Count > 0 && CurrentIndex == Steps.Count - 1;

    public bool HasDetailLines => CurrentStep?.DetailLines.Count > 0;

    public string ProgressText => string.Format(
        CultureInfo.CurrentCulture,
        Strings.Onboarding_Progress_Format,
        CurrentIndex + 1,
        Steps.Count);

    public string NextButtonText =>
        IsLastStep ? Strings.Onboarding_Nav_Finish : Strings.Onboarding_Nav_Next;

    public string BackButtonText => Strings.Onboarding_Nav_Back;

    public string SkipButtonText => Strings.Onboarding_Nav_Skip;

    public string WindowTitleText => Strings.Onboarding_WindowTitle;

    /// <summary>
    /// (Re)starts the tour: rebuilds the steps from the current hotkey
    /// bindings and activates the first one.
    /// </summary>
    public void Start()
    {
        Steps.Clear();
        foreach (var step in OnboardingStepFactory.Build(_hotkeyService?.Bindings))
            Steps.Add(new OnboardingStepItemViewModel(step));

        if (CurrentIndex != 0)
        {
            CurrentIndex = 0; // change notification runs ApplyStep
            return;
        }

        // Index already 0 (first launch, or a re-launch that ended on step 0):
        // the setter won't fire, so raise the notifications and apply by hand
        // so the view still re-presents the first step.
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(HasDetailLines));
        ApplyStep(0);
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLastStep)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (CurrentIndex < Steps.Count - 1)
            CurrentIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentIndex > 0)
            CurrentIndex--;
    }

    [RelayCommand]
    private void Skip() => CloseRequested?.Invoke(this, EventArgs.Empty);

    partial void OnCurrentIndexChanged(int value) => ApplyStep(value);

    private void ApplyStep(int index)
    {
        for (var i = 0; i < Steps.Count; i++)
            Steps[i].IsCurrent = i == index;

        ActivateCurrentStep();
    }

    private void ActivateCurrentStep()
    {
        switch (CurrentStep?.TargetWindow)
        {
            case OnboardingTargetWindow.Transcribe:
                // Don't steal focus from the wizard — the user is reading it.
                _windowManager.ShowTranscribe(activate: false);
                break;
            case OnboardingTargetWindow.Settings:
                _windowManager.ShowSettings(CurrentStep.SettingsSection);
                break;
        }
    }
}
