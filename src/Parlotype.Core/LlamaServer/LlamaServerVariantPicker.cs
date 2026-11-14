namespace Parlotype.Core.LlamaServer;

/// <summary>
/// Chooses a <c>llama-server</c> build without asking the user, for features
/// that set the server up on their behalf (speech cleanup, ADR-073).
/// </summary>
public static class LlamaServerVariantPicker
{
    /// <summary>
    /// Returns the newest CPU build in <paramref name="groups"/>, or null when
    /// the catalog offers none.
    /// </summary>
    /// <remarks>
    /// CPU on purpose. The models these features run are small enough for a
    /// laptop CPU, a CPU build is a few tens of megabytes with no driver or
    /// runtime to match, and it starts on every machine. Picking a GPU build
    /// automatically would mean guessing the vendor and the CUDA version, and a
    /// wrong guess fails at launch. Someone who wants GPU inference installs
    /// that build from the llama.cpp server page. The catalog is already
    /// filtered to this OS and architecture and ordered newest first.
    /// </remarks>
    public static LlamaServerVariant? PickCpuVariant(IReadOnlyList<LlamaServerReleaseGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        foreach (var group in groups)
        {
            var cpu = group.Variants.FirstOrDefault(v => v.Backend == LlamaServerBackend.Cpu);
            if (cpu is not null)
                return cpu;
        }

        return null;
    }
}
