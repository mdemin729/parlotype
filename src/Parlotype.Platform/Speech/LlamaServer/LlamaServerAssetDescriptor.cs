using Parlotype.Core.Speech.LlamaServer;

namespace Parlotype.Platform.Speech.LlamaServer;

/// <summary>
/// Parser-only projection of a GitHub release asset filename. Internal because
/// callers should consume the catalog's <see cref="LlamaServerVariant"/> output,
/// not raw parse results. <see cref="Build"/> is null for cudart companions
/// (which omit the build tag from their filename) and <see cref="CudaVersion"/>
/// carries the CUDA toolkit version for CUDA/cudart assets so the catalog can
/// pair them.
/// </summary>
internal sealed record LlamaServerAssetDescriptor(
    string? Build,
    LlamaServerBackend Backend,
    LlamaServerOs Os,
    LlamaServerArch Arch,
    string? CudaVersion,
    bool IsCompanion);
