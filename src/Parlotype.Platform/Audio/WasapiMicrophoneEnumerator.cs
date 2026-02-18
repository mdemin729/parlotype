using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Parlotype.Core.Audio;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Enumerates Windows audio capture devices via WASAPI and notifies on changes.
/// </summary>
public sealed class WasapiMicrophoneEnumerator : IMicrophoneEnumerator, IMMNotificationClient
{
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public event EventHandler? DevicesChanged;

    public WasapiMicrophoneEnumerator()
    {
        _enumerator = new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var defaultId = GetDefaultDeviceId();
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        return devices
            .Select(d => new MicrophoneInfo(d.ID, d.FriendlyName, d.ID == defaultId))
            .ToList();
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
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId)
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Capture)
        {
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
