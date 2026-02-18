namespace Parlotype.Core.Audio;

/// <summary>
/// Enumerates available audio capture devices and notifies when devices change.
/// </summary>
public interface IMicrophoneEnumerator : IDisposable
{
    /// <summary>Returns all currently available capture devices.</summary>
    IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones();

    /// <summary>Returns the system default capture device, or null if none available.</summary>
    MicrophoneInfo? GetDefaultMicrophone();

    /// <summary>Raised when devices are added, removed, or change state.</summary>
    event EventHandler DevicesChanged;
}
