namespace Parlotype.Core.Speech;

/// <summary>
/// How much audio each engine may be handed in a single recognizer call.
/// </summary>
/// <remarks>
/// <para>
/// These are not cost guards — for Parakeet the limit is a correctness one. Measured
/// on the synthetic duration ladder (<c>plans/2026-08-18-hold-scoped-push-to-talk/research.md</c>),
/// Parakeet TDT v3 via sherpa-onnx silently drops text as a single decode grows —
/// 100 % of words at 60 s, 60 % at 120 s, 48 % at 300 s — and then crashes outright
/// above 400 s with an <c>SEHException</c> out of native ONNX Runtime, where a
/// 5000-frame positional-encoding buffer (80 ms per frame) runs out. So the usable
/// Parakeet ceiling is the last clean rung, not the crash point.
/// </para>
/// <para>
/// Whisper showed no such knee — 99 % word retention and flat WER out to 600 s,
/// because whisper.cpp chunks internally at 30 s. Its ceiling is a latency choice:
/// a hold longer than this leaves the user staring at one long decode.
/// </para>
/// <para>
/// Gemma 4 and the two cloud engines are <b>unmeasured</b> and take the conservative
/// Parakeet ceiling. That is never worse than what shipped before this type existed,
/// when every engine was chopped at the pipeline's flat 30 s buffer cap.
/// </para>
/// </remarks>
public static class SpeechEngineLimits
{
    /// <summary>Parakeet's last rung with full word retention; well under the 400 s crash point.</summary>
    public const int ParakeetMaxUtteranceSeconds = 60;

    /// <summary>Whisper tolerates more; this is a latency ceiling, not a quality one.</summary>
    public const int WhisperMaxUtteranceSeconds = 300;

    /// <summary>Applied to engines with no long-audio measurements of their own.</summary>
    public const int UnmeasuredMaxUtteranceSeconds = ParakeetMaxUtteranceSeconds;

    /// <summary>
    /// Longest single recognizer call, in seconds, that <paramref name="engine"/> handles
    /// without losing text. Audio beyond this must be split — on speech boundaries, so the
    /// cut lands in a pause rather than mid-word.
    /// </summary>
    public static int MaxUtteranceSeconds(SpeechEngine engine) => engine switch
    {
        SpeechEngine.Parakeet => ParakeetMaxUtteranceSeconds,
        SpeechEngine.Whisper => WhisperMaxUtteranceSeconds,
        _ => UnmeasuredMaxUtteranceSeconds,
    };
}
