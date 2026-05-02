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

    public void Dispose() { }
}
