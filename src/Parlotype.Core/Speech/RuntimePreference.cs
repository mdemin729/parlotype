namespace Parlotype.Core.Speech;

/// <summary>
/// Controls which Whisper.net runtime backend to prefer.
/// Must be configured before the first WhisperFactory is created.
/// </summary>
public enum RuntimePreference
{
    /// <summary>
    /// Try the Vulkan GPU runtime first and fall back to CPU if it isn't
    /// loadable. This is the default.
    /// </summary>
    Auto,

    /// <summary>
    /// Force Vulkan only. No fallback — Whisper initialization will fail
    /// with <see cref="RuntimeUnavailableException"/> if the Vulkan loader
    /// or a Vulkan-capable device isn't available.
    /// </summary>
    Vulkan,

    /// <summary>Force CPU-only inference, ignoring any available GPU.</summary>
    Cpu
}
