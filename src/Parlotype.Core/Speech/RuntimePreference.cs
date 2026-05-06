namespace Parlotype.Core.Speech;

/// <summary>
/// Controls which Whisper.net runtime backend to prefer.
/// Must be configured before the first WhisperFactory is created.
/// </summary>
public enum RuntimePreference
{
    /// <summary>
    /// Try GPU runtimes in order — CUDA, then Vulkan — and fall back to CPU
    /// if neither is loadable. This is the default.
    /// </summary>
    Auto,

    /// <summary>
    /// Force NVIDIA CUDA only. No fallback — Whisper initialization will fail
    /// with <see cref="RuntimeUnavailableException"/> if CUDA isn't usable.
    /// </summary>
    Cuda,

    /// <summary>
    /// Force Vulkan only. No fallback — Whisper initialization will fail
    /// with <see cref="RuntimeUnavailableException"/> if the Vulkan loader
    /// or a Vulkan-capable device isn't available.
    /// </summary>
    Vulkan,

    /// <summary>Force CPU-only inference, ignoring any available GPU.</summary>
    Cpu
}
