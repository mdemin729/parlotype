namespace Parlotype.Core.Speech;

/// <summary>
/// Read-only view of the Whisper native runtime that the current process has
/// actually loaded. Runtime selection is process-wide and one-shot (ADR-012,
/// ADR-022): once a model has been loaded, changing
/// <see cref="RuntimePreference"/> only takes effect after an app restart.
/// The UI uses this to tell the user a restart is pending instead of letting
/// them hit a failed recording start.
/// </summary>
public interface IWhisperRuntimeStatus
{
    /// <summary>
    /// Name of the runtime library the process loaded ("Cuda", "Vulkan", "Cpu", …),
    /// or <c>null</c> when no Whisper model has been loaded yet.
    /// </summary>
    string? LoadedRuntimeName { get; }

    /// <summary>
    /// True when a runtime is already loaded and it does not satisfy
    /// <paramref name="preference"/> — i.e. applying that preference needs a restart.
    /// </summary>
    bool RequiresRestartFor(RuntimePreference preference);
}
