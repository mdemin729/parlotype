using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Windows implementation of <see cref="INvidiaEnvironmentProvider"/>.
/// Combines three independent detection sources, each isolated so a failure
/// in one doesn't break the others. Caches the first detection result.
/// </summary>
internal sealed partial class WindowsNvidiaEnvironmentProvider : INvidiaEnvironmentProvider
{
    private static readonly TimeSpan NvidiaSmiTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<WindowsNvidiaEnvironmentProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NvidiaEnvironmentInfo? _cached;

    public WindowsNvidiaEnvironmentProvider(ILogger<WindowsNvidiaEnvironmentProvider> logger)
    {
        _logger = logger;
    }

    public async Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _cached ??= await DetectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cached = await DetectAsync(cancellationToken).ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<NvidiaEnvironmentInfo> DetectAsync(CancellationToken cancellationToken)
    {
        var (driverVersion, driverMaxCuda) = await TryDetectDriverAsync(cancellationToken).ConfigureAwait(false);
        var toolkits = TryDetectToolkits();
        var runtimes = TryDetectLoadableRuntimes();

        return new NvidiaEnvironmentInfo
        {
            DriverVersion = driverVersion,
            DriverMaxCudaVersion = driverMaxCuda,
            InstalledToolkitVersions = toolkits,
            LoadableRuntimes = runtimes,
        };
    }

    // ----- Source 1: nvidia-smi --------------------------------------------

    private async Task<(string? driverVersion, string? maxCuda)> TryDetectDriverAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await RunNvidiaSmiAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output))
                return (null, null);

            return ParseNvidiaSmiOutput(output);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "nvidia-smi invocation failed");
            return (null, null);
        }
    }

    private static async Task<string?> RunNvidiaSmiAsync(CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("nvidia-smi")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
            return null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(NvidiaSmiTimeout);

        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return process.ExitCode == 0 ? stdout : null;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }
    }

    internal static (string? driverVersion, string? maxCuda) ParseNvidiaSmiOutput(string output)
    {
        // Example header line:
        //   "| NVIDIA-SMI 596.36                 Driver Version: 596.36         CUDA Version: 13.2     |"
        var driverMatch = DriverVersionRegex().Match(output);
        var cudaMatch = CudaVersionRegex().Match(output);

        var driver = driverMatch.Success ? driverMatch.Groups[1].Value : null;
        var cuda = cudaMatch.Success ? cudaMatch.Groups[1].Value : null;
        return (driver, cuda);
    }

    [GeneratedRegex(@"Driver Version:\s*([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DriverVersionRegex();

    [GeneratedRegex(@"CUDA Version:\s*([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CudaVersionRegex();

    // ----- Source 2: filesystem toolkit scan -------------------------------

    private IReadOnlyList<string> TryDetectToolkits()
    {
        try
        {
            var versions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // Scan the standard install location.
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var cudaRoot = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
                if (Directory.Exists(cudaRoot))
                {
                    foreach (var dir in Directory.EnumerateDirectories(cudaRoot, "v*"))
                    {
                        var name = Path.GetFileName(dir);
                        if (!string.IsNullOrEmpty(name) && name.StartsWith('v'))
                            versions.Add(name[1..]);
                    }
                }
            }

            // Honor CUDA_PATH and CUDA_PATH_V* environment variables.
            foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
            {
                if (entry.Key is not string key)
                    continue;

                if (!key.StartsWith("CUDA_PATH", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.Value is not string value || string.IsNullOrEmpty(value))
                    continue;

                var version = ExtractVersionFromCudaPath(value);
                if (version is not null)
                    versions.Add(version);
            }

            return [.. versions];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CUDA toolkit detection failed");
            return [];
        }
    }

    internal static string? ExtractVersionFromCudaPath(string path)
    {
        // Expected shape: "...\NVIDIA GPU Computing Toolkit\CUDA\v13.2"
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = Path.GetFileName(trimmed);
        if (!string.IsNullOrEmpty(leaf) && leaf.StartsWith('v') && leaf.Length > 1
            && char.IsDigit(leaf[1]))
        {
            return leaf[1..];
        }
        return null;
    }

    // ----- Source 3: cudart P/Invoke ---------------------------------------

    private IReadOnlyList<CudaRuntimeProbe> TryDetectLoadableRuntimes()
    {
        var probes = new List<CudaRuntimeProbe>();
        foreach (var libraryName in new[] { "cudart64_13", "cudart64_12" })
        {
            try
            {
                if (!NativeLibrary.TryLoad(libraryName, out var handle))
                    continue;

                try
                {
                    var probe = ProbeCudart(libraryName, handle);
                    if (probe is not null)
                        probes.Add(probe);
                }
                finally
                {
                    NativeLibrary.Free(handle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to probe {Library}", libraryName);
            }
        }

        return probes;
    }

    private static CudaRuntimeProbe? ProbeCudart(string libraryName, IntPtr handle)
    {
        var runtimeVer = TryGetCudartVersion(handle, "cudaRuntimeGetVersion");
        var driverVer = TryGetCudartVersion(handle, "cudaDriverGetVersion");
        if (runtimeVer is null && driverVer is null)
            return null;

        return new CudaRuntimeProbe(
            libraryName,
            runtimeVer ?? "unknown",
            driverVer ?? "unknown");
    }

    private static string? TryGetCudartVersion(IntPtr handle, string symbolName)
    {
        if (!NativeLibrary.TryGetExport(handle, symbolName, out var exportPtr))
            return null;

        var fn = Marshal.GetDelegateForFunctionPointer<CudaGetVersionDelegate>(exportPtr);
        var status = fn(out var raw);
        if (status != 0)
            return null;

        return DecodeCudaVersion(raw);
    }

    /// <summary>Decodes a CUDA version integer (e.g. 13020) into "major.minor" (e.g. "13.2").</summary>
    internal static string DecodeCudaVersion(int raw)
    {
        var major = raw / 1000;
        var minor = raw % 1000 / 10;
        return string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CudaGetVersionDelegate(out int version);
}
