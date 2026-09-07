using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class HotkeySettingsViewModel : SettingsSectionViewModelBase
{
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly ISettingsService _settings;
    private readonly ILogger<HotkeySettingsViewModel> _logger;

    public override string Title => Strings.Settings_Hotkeys_Title;
    public override SettingsCategory Category => SettingsCategory.Input;

    /// <summary>The configured gestures, in list order.</summary>
    public ObservableCollection<HotkeyBindingItemViewModel> Bindings { get; } = [];

    /// <summary>
    /// Ready-made gestures offered by the Add button. A key-capture field can
    /// record a chord, but it has no way to express "hold this modifier" or
    /// "tap it twice", so those arrive as presets.
    /// </summary>
    public IReadOnlyList<HotkeyPresetViewModel> Presets { get; }

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recorderText = Strings.Settings_Hotkeys_Recorder_Idle;

    /// <summary>Reserved-shortcut or duplicate message; the offending binding was rejected.</summary>
    [ObservableProperty]
    private string? _blockingWarning;

    /// <summary>Advisory note about a binding that was accepted anyway.</summary>
    [ObservableProperty]
    private string? _advisoryWarning;

    /// <summary>
    /// The candidate + conflict behind whichever of <see cref="BlockingWarning"/>
    /// / <see cref="AdvisoryWarning"/> is currently set, so
    /// <see cref="OnCultureChanged"/> can re-render the message instead of
    /// leaving it in the language it was first shown in (ADR-064 amendment).
    /// Null whenever both warnings are clear.
    /// </summary>
    private (DictationHotkey Candidate, HotkeyConflict Conflict)? _lastWarning;

    public bool HasBindings => Bindings.Count > 0;

    public HotkeySettingsViewModel(
        IGlobalHotkeyService? hotkeyService,
        ISettingsService settings,
        ILogger<HotkeySettingsViewModel>? logger = null)
    {
        _hotkeyService = hotkeyService;
        _settings = settings;
        _logger = logger ?? NullLogger<HotkeySettingsViewModel>.Instance;

        Presets =
        [
            new HotkeyPresetViewModel(DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Right), AddPresetCommand),
            new HotkeyPresetViewModel(DictationHotkey.Hold(ModifierKey.Alt, ModifierSide.Right), AddPresetCommand),
            new HotkeyPresetViewModel(DictationHotkey.DoubleTap(ModifierKey.Ctrl, ModifierSide.Either), AddPresetCommand),
            new HotkeyPresetViewModel(DictationHotkey.DoubleTap(ModifierKey.Shift, ModifierSide.Either), AddPresetCommand),
        ];

        Bindings.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBindings));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var bindings = _hotkeyService?.Bindings is { Count: > 0 } live
                ? live
                : await HotkeySettingsMigrator.LoadOrMigrateAsync(_settings);

            Reload(bindings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hotkey settings");
            Reload(DictationHotkeyDefaults.All);
        }
    }

    /// <summary>Refreshes the list from the hotkey service — used when the settings flyout opens.</summary>
    public void Refresh()
    {
        if (_hotkeyService?.Bindings is { Count: > 0 } bindings)
            Reload(bindings);
    }

    private void Reload(IReadOnlyList<DictationHotkey> bindings)
    {
        Bindings.Clear();
        foreach (var binding in bindings)
            Bindings.Add(CreateItem(binding));
    }

    private HotkeyBindingItemViewModel CreateItem(DictationHotkey hotkey) =>
        new(hotkey, RemoveBindingCommand, ToggleBindingModeCommand);

    [RelayCommand]
    private void StartRecording()
    {
        IsRecording = true;
        RecorderText = Strings.Settings_Hotkeys_Recorder_Listening;
        ClearWarnings();
    }

    [RelayCommand]
    private void StopRecording()
    {
        IsRecording = false;
        RecorderText = Strings.Settings_Hotkeys_Recorder_Idle;
    }

    [RelayCommand]
    private void AddPreset(DictationHotkey? hotkey)
    {
        if (hotkey is not null)
            TryAdd(hotkey);
    }

    [RelayCommand]
    private void RemoveBinding(HotkeyBindingItemViewModel? item)
    {
        if (item is null || !Bindings.Remove(item))
            return;

        ClearWarnings();
        Commit();
    }

    /// <summary>Flips a chord between push-to-talk and toggle.</summary>
    [RelayCommand]
    private void ToggleBindingMode(HotkeyBindingItemViewModel? item)
    {
        if (item is null || !item.CanChangeMode)
            return;

        var index = Bindings.IndexOf(item);
        if (index < 0)
            return;

        var flipped = item.Hotkey.Mode == ActivationMode.PushToTalk
            ? ActivationMode.Toggle
            : ActivationMode.PushToTalk;

        Bindings[index] = CreateItem(item.Hotkey with { Mode = flipped });
        Commit();
    }

    /// <summary>Called by the view once it has captured a chord from the keyboard.</summary>
    public void ApplyRecordedChord(HotkeyBinding chord)
    {
        StopRecording();

        if (!chord.IsValid)
            return;

        TryAdd(DictationHotkey.Chord(chord, ActivationMode.Toggle));
    }

    private void TryAdd(DictationHotkey candidate)
    {
        ClearWarnings();

        var existing = Bindings.Select(b => b.Hotkey).ToList();
        var conflict = HotkeyConflictDetector.Check(candidate, existing);

        // The warning the user reads is built here from the conflict's reason;
        // conflict.Description is the invariant form, which is what the log wants.
        if (conflict.IsBlocking)
        {
            _lastWarning = (candidate, conflict);
            BlockingWarning = HotkeyText.Conflict(candidate, conflict);
            _logger.LogInformation("Rejected hotkey {Binding}: {Reason}",
                candidate.DisplayString, conflict.Description);
            return;
        }

        if (conflict.HasMessage)
        {
            _lastWarning = (candidate, conflict);
            AdvisoryWarning = HotkeyText.Conflict(candidate, conflict);
        }

        Bindings.Add(CreateItem(candidate));
        Commit();
    }

    private void ClearWarnings()
    {
        BlockingWarning = null;
        AdvisoryWarning = null;
        _lastWarning = null;
    }

    /// <summary>
    /// Existing rows compute <c>DisplayString</c>/<c>ModeLabel</c> fresh on every
    /// read but raise nothing on their own, the recorder prompt and any warning
    /// are stored strings rather than <c>{loc:Tr}</c> bindings, and the "Add"
    /// menu's preset rows are bound once by a flyout that reuses its item
    /// containers — so a live language switch has to touch all three
    /// (ADR-064 amendment).
    /// </summary>
    protected override void OnCultureChanged()
    {
        base.OnCultureChanged();

        RecorderText = IsRecording
            ? Strings.Settings_Hotkeys_Recorder_Listening
            : Strings.Settings_Hotkeys_Recorder_Idle;

        if (_lastWarning is { } warning)
        {
            var message = HotkeyText.Conflict(warning.Candidate, warning.Conflict);
            if (warning.Conflict.IsBlocking)
                BlockingWarning = message;
            else
                AdvisoryWarning = message;
        }

        foreach (var binding in Bindings)
            binding.RefreshDisplay();

        foreach (var preset in Presets)
            preset.RefreshDisplay();
    }

    private void Commit()
    {
        var bindings = Bindings.Select(b => b.Hotkey).ToList();

        if (_hotkeyService is not null)
        {
            _hotkeyService.UpdateBindings(bindings);
            return;
        }

        // No hook running (design mode, headless tests) — persist directly so
        // the choice still survives a restart.
        _ = PersistAsync(bindings);
    }

    private async Task PersistAsync(IReadOnlyList<DictationHotkey> bindings)
    {
        try
        {
            await _settings.SetAsync(SettingsKeys.HotkeyBindings, HotkeyBindingCodec.EncodeAll(bindings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hotkey bindings");
        }
    }
}
