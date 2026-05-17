namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// Compute backend a llama.cpp server build targets.
/// <see cref="Unknown"/> covers asset variants the parser does not recognise
/// (e.g. future backends, deprecated builds); such variants are filtered out
/// of the catalog but kept as an enum value to keep parsing tolerant.
/// </summary>
public enum LlamaServerBackend
{
    Unknown = 0,
    Cpu,
    Cuda12,
    Cuda13,
    Vulkan,
    Hip,
    Sycl,
    Metal,
    KleidiAi,
}
