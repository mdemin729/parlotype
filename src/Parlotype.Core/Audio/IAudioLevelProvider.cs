namespace Parlotype.Core.Audio;

/// <summary>
/// Provides real-time audio level (RMS amplitude) for UI visualisation.
/// </summary>
public interface IAudioLevelProvider
{
    /// <summary>Current RMS level in the range [0.0, 1.0].</summary>
    float CurrentLevel { get; }

    /// <summary>Raised when a new audio level measurement is available.</summary>
    event EventHandler<AudioLevelEventArgs> LevelChanged;
}

/// <summary>Carries an RMS audio level measurement.</summary>
public sealed class AudioLevelEventArgs : EventArgs
{
    /// <summary>RMS amplitude in the range [0.0, 1.0].</summary>
    public required float Level { get; init; }
}
