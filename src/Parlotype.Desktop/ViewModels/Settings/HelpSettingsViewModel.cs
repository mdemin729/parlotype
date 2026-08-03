using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Settings → Help (ADR-055): re-launches the onboarding tour and lists the
/// currently configured dictation hotkeys, kept fresh via
/// <see cref="IGlobalHotkeyService.BindingsChanged"/>. All copy comes from
/// <see cref="Strings"/> so the view holds no text of its own.
/// </summary>
public sealed partial class HelpSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly IOnboardingService _onboarding;
    private readonly IGlobalHotkeyService? _hotkeyService;

    public HelpSettingsViewModel(
        IOnboardingService onboarding,
        IGlobalHotkeyService? hotkeyService = null)
    {
        _onboarding = onboarding;
        _hotkeyService = hotkeyService;

        if (_hotkeyService is not null)
            _hotkeyService.BindingsChanged += OnBindingsChanged;

        RebuildHotkeyLines();
    }

    public override string Title => Strings.Help_Title;

    public override SettingsCategory Category => SettingsCategory.Application;

    public string IntroText => Strings.Help_Intro;

    public string OpenTourButtonText => Strings.Help_OpenTourButton;

    public string HotkeysHeadingText => Strings.Help_HotkeysHeading;

    /// <summary>
    /// One display line per valid binding ("Hold Right Ctrl — Push to talk")
    /// plus the Esc-cancel line, or the no-hotkeys fallback when the user has
    /// removed every binding (an empty list is a deliberate choice, ADR-047).
    /// </summary>
    public ObservableCollection<string> HotkeyLines { get; } = [];

    [RelayCommand]
    private void OpenTour() => _onboarding.ShowWizard();

    private void OnBindingsChanged(object? sender, EventArgs e)
    {
        // The hotkey service raises from its own dispatch context; collection
        // mutations must land on the UI thread.
        if (Dispatcher.UIThread.CheckAccess())
            RebuildHotkeyLines();
        else
            Dispatcher.UIThread.Post(RebuildHotkeyLines);
    }

    private void RebuildHotkeyLines()
    {
        HotkeyLines.Clear();

        var validBindings = _hotkeyService?.Bindings.Where(b => b.IsValid).ToList() ?? [];
        if (validBindings.Count == 0)
        {
            HotkeyLines.Add(Strings.Help_NoHotkeys);
            return;
        }

        foreach (var binding in validBindings)
            HotkeyLines.Add($"{binding.DisplayString} — {binding.ModeLabel}");
        HotkeyLines.Add(Strings.Help_EscCancelLine);
    }
}
