using Parlotype.Core.LlamaServer;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// Pure, allocation-light parser for llama.cpp release asset filenames.
/// Returns false only for names that are clearly not llama.cpp release
/// artefacts (e.g. <c>source.zip</c>). Recognised-but-uncategorised variants
/// (unknown backend, unknown OS, unknown arch) parse successfully with the
/// relevant fields set to <c>Unknown</c>; the catalog filters those out.
/// </summary>
internal static class LlamaServerAssetParser
{
    private const string ZipExt = ".zip";
    private const string TarGzExt = ".tar.gz";

    public static bool TryParse(string assetName, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        var (stem, ext) = StripExtension(assetName);
        if (ext is null)
            return false;

        var parts = stem.Split('-');
        if (parts.Length < 4)
            return false;

        // Companion: cudart-llama-bin-win-cuda-{version}-{arch}.zip
        if (parts[0] == "cudart")
            return TryParseCompanion(parts, ext, out descriptor);

        if (parts[0] != "llama" || parts[2] != "bin")
            return false;

        var build = parts[1];
        if (!IsValidBuild(build))
            return false;

        var platform = parts[3];
        var tail = parts[4..];

        return platform switch
        {
            "win" => TryParseWindows(build, tail, out descriptor),
            "macos" => TryParseMacOs(build, tail, out descriptor),
            "ubuntu" => TryParseLinux(build, tail, out descriptor),
            "android" => TryParseAndroid(build, tail, out descriptor),
            _ => TryParseUnknownPlatform(build, out descriptor),
        };
    }

    private static bool TryParseCompanion(string[] parts, string ext, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (ext != ZipExt) return false;
        // cudart-llama-bin-win-cuda-{version}-{arch}
        if (parts.Length != 7) return false;
        if (parts[1] != "llama" || parts[2] != "bin" || parts[3] != "win" || parts[4] != "cuda")
            return false;

        descriptor = new LlamaServerAssetDescriptor(
            Build: null,
            Backend: CudaBackendFromVersion(parts[5]),
            Os: LlamaServerOs.Windows,
            Arch: ParseArch(parts[6]),
            CudaVersion: parts[5],
            IsCompanion: true);
        return true;
    }

    private static bool TryParseWindows(string build, string[] tail, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (tail.Length < 2) return false;

        switch (tail[0])
        {
            case "cpu":
                descriptor = new(build, LlamaServerBackend.Cpu, LlamaServerOs.Windows, ParseArch(tail[1]), null, false);
                return true;
            case "vulkan":
                descriptor = new(build, LlamaServerBackend.Vulkan, LlamaServerOs.Windows, ParseArch(tail[1]), null, false);
                return true;
            case "sycl":
                descriptor = new(build, LlamaServerBackend.Sycl, LlamaServerOs.Windows, ParseArch(tail[1]), null, false);
                return true;
            case "cuda":
                if (tail.Length < 3) return false;
                descriptor = new(build, CudaBackendFromVersion(tail[1]), LlamaServerOs.Windows, ParseArch(tail[2]), tail[1], false);
                return true;
            case "hip":
                // hip-radeon-x64 or hip-x64 (future-proof)
                var arch = tail.Length >= 3 ? ParseArch(tail[^1]) : ParseArch(tail[1]);
                descriptor = new(build, LlamaServerBackend.Hip, LlamaServerOs.Windows, arch, null, false);
                return true;
            default:
                descriptor = new(build, LlamaServerBackend.Unknown, LlamaServerOs.Windows, LlamaServerArch.Unknown, null, false);
                return true;
        }
    }

    private static bool TryParseMacOs(string build, string[] tail, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (tail.Length < 1) return false;

        var arch = ParseArch(tail[0]);
        LlamaServerBackend backend;
        if (tail.Length > 1 && tail[1] == "kleidiai")
            backend = LlamaServerBackend.KleidiAi;
        else if (arch == LlamaServerArch.Arm64)
            backend = LlamaServerBackend.Metal;
        else
            backend = LlamaServerBackend.Cpu;

        descriptor = new(build, backend, LlamaServerOs.MacOs, arch, null, false);
        return true;
    }

    private static bool TryParseLinux(string build, string[] tail, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (tail.Length < 1) return false;

        switch (tail[0])
        {
            case "vulkan":
                if (tail.Length < 2) return false;
                descriptor = new(build, LlamaServerBackend.Vulkan, LlamaServerOs.Linux, ParseArch(tail[1]), null, false);
                return true;
            case "rocm":
                if (tail.Length < 3) return false;
                descriptor = new(build, LlamaServerBackend.Hip, LlamaServerOs.Linux, ParseArch(tail[^1]), null, false);
                return true;
            case "sycl":
                if (tail.Length < 3) return false;
                descriptor = new(build, LlamaServerBackend.Sycl, LlamaServerOs.Linux, ParseArch(tail[^1]), null, false);
                return true;
            case "openvino":
                if (tail.Length < 3) return false;
                descriptor = new(build, LlamaServerBackend.Unknown, LlamaServerOs.Linux, ParseArch(tail[^1]), null, false);
                return true;
            default:
                // Plain CPU build: just an arch
                descriptor = new(build, LlamaServerBackend.Cpu, LlamaServerOs.Linux, ParseArch(tail[0]), null, false);
                return true;
        }
    }

    private static bool TryParseAndroid(string build, string[] tail, out LlamaServerAssetDescriptor descriptor)
    {
        descriptor = default!;
        if (tail.Length < 1) return false;
        // Android is not a supported OS in our enum; mark Os.Unknown so the catalog filters it.
        descriptor = new(build, LlamaServerBackend.Cpu, LlamaServerOs.Unknown, ParseArch(tail[0]), null, false);
        return true;
    }

    private static bool TryParseUnknownPlatform(string build, out LlamaServerAssetDescriptor descriptor)
    {
        // openEuler (310p, 910b) and any future platform fall here.
        descriptor = new(build, LlamaServerBackend.Unknown, LlamaServerOs.Unknown, LlamaServerArch.Unknown, null, false);
        return true;
    }

    private static LlamaServerArch ParseArch(string s) => s.ToLowerInvariant() switch
    {
        "x64" => LlamaServerArch.X64,
        "arm64" => LlamaServerArch.Arm64,
        _ => LlamaServerArch.Unknown,
    };

    private static LlamaServerBackend CudaBackendFromVersion(string version) =>
        version.StartsWith("12", StringComparison.Ordinal) ? LlamaServerBackend.Cuda12
        : version.StartsWith("13", StringComparison.Ordinal) ? LlamaServerBackend.Cuda13
        : LlamaServerBackend.Unknown;

    private static bool IsValidBuild(string build)
    {
        if (build.Length < 2 || build[0] != 'b') return false;
        for (var i = 1; i < build.Length; i++)
            if (!char.IsDigit(build[i])) return false;
        return true;
    }

    private static (string Stem, string? Ext) StripExtension(string name)
    {
        if (name.EndsWith(TarGzExt, StringComparison.OrdinalIgnoreCase))
            return (name[..^TarGzExt.Length], TarGzExt);
        if (name.EndsWith(ZipExt, StringComparison.OrdinalIgnoreCase))
            return (name[..^ZipExt.Length], ZipExt);
        return (name, null);
    }
}
