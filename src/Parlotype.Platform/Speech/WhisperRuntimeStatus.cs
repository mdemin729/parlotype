using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Exposes <see cref="WhisperRuntimeBootstrap"/>'s process-wide runtime state to
/// layers that cannot see Whisper.net types (Desktop). See <see cref="IWhisperRuntimeStatus"/>.
/// </summary>
public sealed class WhisperRuntimeStatus : IWhisperRuntimeStatus
{
    public string? LoadedRuntimeName => WhisperRuntimeBootstrap.LoadedRuntime?.ToString();

    public bool RequiresRestartFor(RuntimePreference preference)
        => !WhisperRuntimeBootstrap.IsSatisfiedBy(preference, WhisperRuntimeBootstrap.LoadedRuntime);
}
