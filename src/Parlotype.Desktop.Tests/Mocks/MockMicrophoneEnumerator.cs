using Parlotype.Core.Audio;

namespace Parlotype.Desktop.Tests.Mocks;

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

    public void RaiseChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Replaces the device list and announces it, as a hot-plug (or a late first enumeration) does.</summary>
    public void SetDevices(params MicrophoneInfo[] devices)
    {
        _devices.Clear();
        _devices.AddRange(devices);
        RaiseChanged();
    }

    public void Dispose() { }
}
