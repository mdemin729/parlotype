namespace Parlotype.Core.Audio;

/// <summary>
/// Visual state of the recording / waveform control.
/// </summary>
public enum RecordingState
{
    /// <summary>Speech recognition is disabled — show microphone icon.</summary>
    Disabled,

    /// <summary>The speech model is loading — show a spinner until recording can begin.</summary>
    Loading,

    /// <summary>Recording is active but the user is silent — show idle breathing bars.</summary>
    Idle,

    /// <summary>Recording is active and speech is detected — show audio-reactive bars.</summary>
    Active
}
