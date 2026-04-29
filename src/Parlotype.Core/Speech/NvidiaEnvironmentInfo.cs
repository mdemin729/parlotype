namespace Parlotype.Core.Speech;

/// <summary>
/// Information about a CUDA runtime (cudart) library that was successfully loaded
/// and probed. Reports both the runtime API version and the driver API version
/// reported through that runtime.
/// </summary>
/// <param name="LibraryName">Name of the loaded library, e.g. <c>cudart64_13</c>.</param>
/// <param name="RuntimeVersion">CUDA runtime API version (e.g. <c>"13.2"</c>).</param>
/// <param name="DriverVersion">CUDA driver API version reported by this runtime (e.g. <c>"13.2"</c>).</param>
public sealed record CudaRuntimeProbe(string LibraryName, string RuntimeVersion, string DriverVersion);

/// <summary>
/// Snapshot of the NVIDIA / CUDA environment on the current machine.
/// All fields are best-effort — any individual field may be <c>null</c> or empty
/// if the corresponding source could not be queried.
/// </summary>
public sealed record NvidiaEnvironmentInfo
{
    /// <summary>NVIDIA display driver version (e.g. <c>"596.36"</c>), or <c>null</c> if not detected.</summary>
    public string? DriverVersion { get; init; }

    /// <summary>
    /// Maximum CUDA API version supported by the installed driver (e.g. <c>"13.2"</c>).
    /// This is what <c>nvidia-smi</c> reports as "CUDA Version" — it does not imply
    /// any toolkit is installed.
    /// </summary>
    public string? DriverMaxCudaVersion { get; init; }

    /// <summary>
    /// CUDA Toolkit versions found installed on disk (e.g. <c>["12.9", "13.2"]</c>).
    /// Empty when no toolkit installation is detected.
    /// </summary>
    public IReadOnlyList<string> InstalledToolkitVersions { get; init; } = [];

    /// <summary>
    /// CUDA runtime libraries that the current process can load and successfully
    /// query. Empty when no <c>cudart</c> can be loaded.
    /// </summary>
    public IReadOnlyList<CudaRuntimeProbe> LoadableRuntimes { get; init; } = [];

    /// <summary>
    /// True if any signal of NVIDIA hardware/software was detected.
    /// </summary>
    public bool HasNvidia =>
        !string.IsNullOrEmpty(DriverVersion)
        || InstalledToolkitVersions.Count > 0
        || LoadableRuntimes.Count > 0;

    /// <summary>An empty snapshot indicating no NVIDIA environment was detected.</summary>
    public static NvidiaEnvironmentInfo Empty { get; } = new();
}
