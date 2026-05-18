using Parlotype.Core.Speech.LlamaServer;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>Friendly display strings for <see cref="LlamaServerBackend"/>.</summary>
internal static class LlamaServerBackendFormatter
{
    public static string Display(LlamaServerBackend? backend) => backend switch
    {
        LlamaServerBackend.Cpu => "CPU",
        LlamaServerBackend.Cuda12 => "CUDA 12",
        LlamaServerBackend.Cuda13 => "CUDA 13",
        LlamaServerBackend.Vulkan => "Vulkan",
        LlamaServerBackend.Hip => "ROCm / HIP",
        LlamaServerBackend.Sycl => "SYCL",
        LlamaServerBackend.Metal => "Metal",
        LlamaServerBackend.KleidiAi => "KleidiAI",
        LlamaServerBackend.Unknown => "Unknown",
        null => "—",
        _ => backend.ToString() ?? "—",
    };

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        const double mb = 1024.0 * 1024.0;
        if (bytes >= mb * 1000)
            return $"{bytes / (mb * 1024.0):F2} GiB";
        return $"{bytes / mb:F1} MiB";
    }
}
