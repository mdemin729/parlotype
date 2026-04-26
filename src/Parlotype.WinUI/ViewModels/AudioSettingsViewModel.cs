using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.WinUI.ViewModels;

/// <summary>
/// Pairs a <see cref="WaitTimeOption"/> with its human-readable name and duration
/// for display in the Audio Settings UI.
/// </summary>
public sealed record WaitTimeItem(WaitTimeOption Option, string DisplayName, double Seconds)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Manages microphone selection and silence-timeout settings for the Audio page.
/// </summary>
public partial class AudioSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IMicrophoneEnumerator _enumerator;
    private readonly ISettingsService _settings;
    private readonly ILogger<AudioSettingsViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _disposed;

    [ObservableProperty]
    private MicrophoneInfo? _selectedMicrophone;

    [ObservableProperty]
    private WaitTimeItem? _selectedWaitTimeItem;

    public ObservableCollection<MicrophoneInfo> AvailableMicrophones { get; } = [];

    public static WaitTimeItem[] WaitTimeItems { get; } =
    [
        new(WaitTimeOption.Instant,   "Instant (0.1 s)",    0.1),
        new(WaitTimeOption.VeryShort, "Very Short (0.2 s)", 0.2),
        new(WaitTimeOption.Short,     "Short (0.3 s)",      0.3),
        new(WaitTimeOption.Medium,    "Medium (0.5 s)",     0.5),
        new(WaitTimeOption.Long,      "Long (1.0 s)",       1.0),
        new(WaitTimeOption.Extended,  "Extended (2.0 s)",   2.0),
        new(WaitTimeOption.VeryLong,  "Very Long (3.0 s)",  3.0),
    ];

    public AudioSettingsViewModel(
        IMicrophoneEnumerator enumerator,
        ISettingsService settings,
        ILogger<AudioSettingsViewModel>? logger = null)
    {
        _enumerator = enumerator;
        _settings = settings;
        _logger = logger ?? NullLogger<AudioSettingsViewModel>.Instance;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Default selection until persisted value is loaded.
        _selectedWaitTimeItem = WaitTimeItems[3]; // Medium

        _ = InitializeAsync();
    }

    // ── Initialization ───────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            // Load persisted selections in parallel.
            var savedMicIdTask = _settings.GetAsync<string>(SettingsKeys.SelectedMicrophoneId);
            var savedWaitTimeTask = _settings.GetAsync<string>("WaitTimeOption");

            await Task.WhenAll(savedMicIdTask, savedWaitTimeTask);

            var savedMicId = savedMicIdTask.Result;
            var savedWaitTime = savedWaitTimeTask.Result;

            // Populate microphones.
            RefreshMicrophoneList(savedMicId);

            // Restore wait-time selection.
            if (savedWaitTime is not null
                && Enum.TryParse<WaitTimeOption>(savedWaitTime, out var parsed))
            {
                var match = Array.Find(WaitTimeItems, w => w.Option == parsed);
                if (match is not null)
                {
                    SelectedWaitTimeItem = match;
                }
            }

            // Subscribe to hot-plug events.
            _enumerator.DevicesChanged += OnDevicesChanged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialise audio settings");
        }
    }

    // ── Microphone list management ───────────────────────────────────

    private void RefreshMicrophoneList(string? preferredId = null)
    {
        var current = _enumerator.GetAvailableMicrophones();

        AvailableMicrophones.Clear();
        foreach (var mic in current)
        {
            AvailableMicrophones.Add(mic);
        }

        // Try to restore the previously-selected mic.
        preferredId ??= SelectedMicrophone?.Id;

        MicrophoneInfo? best = null;
        if (preferredId is not null)
        {
            best = AvailableMicrophones.FirstOrDefault(m => m.Id == preferredId);
        }

        SelectedMicrophone = best ?? AvailableMicrophones.FirstOrDefault();
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // The event may fire on a background thread; marshal to the UI thread.
        _dispatcherQueue.TryEnqueue(() => RefreshMicrophoneList());
    }

    // ── Commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectMicrophoneAsync(MicrophoneInfo? mic)
    {
        if (mic is null)
            return;

        SelectedMicrophone = mic;

        try
        {
            await _settings.SetAsync(SettingsKeys.SelectedMicrophoneId, mic.Id);
            _logger.LogInformation("Microphone selection saved: {Name} ({Id})", mic.Name, mic.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist microphone selection");
        }
    }

    [RelayCommand]
    private async Task SelectWaitTimeAsync(WaitTimeItem? item)
    {
        if (item is null)
            return;

        SelectedWaitTimeItem = item;

        try
        {
            await _settings.SetAsync("WaitTimeOption", item.Option.ToString());
            _logger.LogInformation("Wait time selection saved: {Option}", item.Option);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist wait-time selection");
        }
    }

    // ── Partial property change hooks ────────────────────────────────

    partial void OnSelectedMicrophoneChanged(MicrophoneInfo? value)
    {
        if (value is not null)
        {
            _logger.LogDebug("Selected microphone changed: {Name}", value.Name);
        }
    }

    // ── Disposal ─────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _enumerator.DevicesChanged -= OnDevicesChanged;
        GC.SuppressFinalize(this);
    }
}
