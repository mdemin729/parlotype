namespace Parlotype.Core.Hotkeys;

/// <summary>Describes the gesture that asked for dictation to begin.</summary>
public sealed class DictationStartEventArgs : EventArgs
{
    /// <summary>
    /// True when the gesture ends the utterance by releasing the key — a hold, or a
    /// chord bound to <see cref="ActivationMode.PushToTalk"/>. Such a session carries
    /// its own end signal, so the pipeline runs as
    /// <see cref="Audio.PipelineMode.SingleUtterance"/> and ignores the silence
    /// timeout (ADR-060). Toggle gestures are false: the user may hold the session
    /// open indefinitely, so silence remains the only cue that a sentence ended.
    /// </summary>
    public required bool HoldScoped { get; init; }

    /// <summary>A start that carries no gesture context — the widget's record button.</summary>
    public static DictationStartEventArgs Toggle { get; } = new() { HoldScoped = false };
}
