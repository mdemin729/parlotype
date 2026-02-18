using Parlotype.Core.Audio;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Controllable mock for <see cref="IMicrophoneEnumerator"/> that allows
/// tests to add/remove microphones and fire DevicesChanged.
/// </summary>
public sealed class MockMicrophoneEnumerator : IMicrophoneEnumerator
{
    private readonly List<MicrophoneInfo> _devices = [];

    public event EventHandler? DevicesChanged;

    public MockMicrophoneEnumerator(params MicrophoneInfo[] initialDevices)
    {
        _devices.AddRange(initialDevices);
    }

    public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones() => _devices.AsReadOnly();

    public MicrophoneInfo? GetDefaultMicrophone() => _devices.FirstOrDefault(d => d.IsDefault);

    public void AddDevice(MicrophoneInfo device)
    {
        _devices.Add(device);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveDevice(string deviceId)
    {
        _devices.RemoveAll(d => d.Id == deviceId);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { }
}
