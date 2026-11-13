namespace Parlotype.Core.LlamaServer;

/// <summary>
/// Chooses a <c>llama-server</c> build without asking the user, for features
/// that set the server up on their behalf (speech cleanup, ADR-073).
/// </summary>
public static class LlamaServerVariantPicker
{
    /// <summary>
    /// Returns the CPU build of the release upstream currently names, or, when
    /// the catalog marks none or that build has no CPU variant, the newest CPU
    /// build. Null when the catalog offers no CPU build at all.
    /// </summary>
    /// <remarks>
    /// The released build first (ADR-074): an install the user did not choose
    /// should be the build upstream designated, the same for everyone until the
    /// next release, not whichever build was pushed in the last hour.
    /// <para>
    /// CPU on purpose. The models these features run are small enough for a
    /// laptop CPU, a CPU build is a few tens of megabytes with no driver or
    /// runtime to match, and it starts on every machine. Picking a GPU build
    /// automatically would mean guessing the vendor and the CUDA version, and a
    /// wrong guess fails at launch. Someone who wants GPU inference installs
    /// that build from the llama.cpp server page. The catalog is already
    /// filtered to this OS and architecture.
    /// </para>
    /// </remarks>
    public static LlamaServerVariant? PickCpuVariant(IReadOnlyList<LlamaServerReleaseGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var released = groups.FirstOrDefault(g => g.ReleaseVersion is not null);
        if (released is not null && CpuVariantOf(released) is { } releasedCpu)
            return releasedCpu;

        foreach (var group in groups)
        {
            if (CpuVariantOf(group) is { } cpu)
                return cpu;
        }

        return null;
    }

    private static LlamaServerVariant? CpuVariantOf(LlamaServerReleaseGroup group) =>
        group.Variants.FirstOrDefault(v => v.Backend == LlamaServerBackend.Cpu);
}
