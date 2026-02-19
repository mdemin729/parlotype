using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Parlotype.Core.Audio;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Enumerates Windows audio capture devices via WASAPI and notifies on changes.
/// </summary>
public sealed class WasapiMicrophoneEnumerator : IMicrophoneEnumerator, IMMNotificationClient
{
    private readonly ILogger<WasapiMicrophoneEnumerator> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public event EventHandler? DevicesChanged;

    public WasapiMicrophoneEnumerator(ILogger<WasapiMicrophoneEnumerator> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(this);
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

    // IMMNotificationClient implementation — all raise DevicesChanged

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
    {
        _logger.LogInformation("Device added: {DeviceId}", pwstrDeviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId)
    {
        _logger.LogInformation("Device removed: {DeviceId}", deviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        _logger.LogInformation("Device state changed: {DeviceId} → {NewState}", deviceId, newState);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Capture)
        {
            _logger.LogInformation("Default capture device changed: {DeviceId}", defaultDeviceId);
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Ignore property changes — they're too frequent and not relevant
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _enumerator.UnregisterEndpointNotificationCallback(this);
    }
}
