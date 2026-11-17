namespace Parlotype.Core.Audio;

/// <summary>
/// The operating system refused access to the microphone, so no capture was
/// started. Distinct from "no microphone" or a device error because the remedy
/// is different: only the user can lift it, in the system's privacy settings
/// (ADR-077).
/// </summary>
public sealed class MicrophoneAccessDeniedException : InvalidOperationException
{
    public MicrophoneAccessDeniedException()
        : base("Microphone access was denied by the operating system.")
    {
    }
}
