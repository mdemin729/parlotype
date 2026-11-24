using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using Parlotype.Core.Audio;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Enumerates Windows audio capture devices via WASAPI and notifies on changes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiMicrophoneEnumerator : IMicrophoneEnumerator
{
    private readonly ILogger<WasapiMicrophoneEnumerator> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDeviceNotificationClient _notifications;
    private bool _disposed;

    public event EventHandler? DevicesChanged;

    public WasapiMicrophoneEnumerator(ILogger<WasapiMicrophoneEnumerator> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
        _notifications = _enumerator.CreateNotificationClient();
        _notifications.DeviceAdded += OnDeviceAdded;
        _notifications.DeviceRemoved += OnDeviceRemoved;
        _notifications.DeviceStateChanged += OnDeviceStateChanged;
        _notifications.DefaultDeviceChanged += OnDefaultDeviceChanged;
        _logger.LogInformation("Microphone enumerator initialized");
    }

    public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var defaultId = GetDefaultDeviceId();
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        var microphones = devices
            .Select(d => new MicrophoneInfo(d.ID, d.FriendlyName, d.ID == defaultId))
            .ToList();

        _logger.LogDebug("Enumerating microphones, found {Count} devices", microphones.Count);

        return microphones;
    }

    public MicrophoneInfo? GetDefaultMicrophone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return new MicrophoneInfo(device.ID, device.FriendlyName, true);
        }
        catch
        {
            return null;
        }
    }

    private string? GetDefaultDeviceId()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID;
        }
        catch
        {
            return null;
        }
    }

    private void OnDeviceAdded(object? sender, DeviceNotificationEventArgs e)
    {
        _logger.LogInformation("Device added: {DeviceId}", e.DeviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeviceRemoved(object? sender, DeviceNotificationEventArgs e)
    {
        _logger.LogInformation("Device removed: {DeviceId}", e.DeviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
    {
        _logger.LogInformation("Device state changed: {DeviceId} → {NewState}", e.DeviceId, e.NewState);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (e.Flow == DataFlow.Capture)
        {
            _logger.LogInformation("Default capture device changed: {DeviceId}", e.DeviceId);
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // PropertyValueChanged is deliberately not subscribed: too frequent and not relevant.
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifications.Dispose();
    }
}
