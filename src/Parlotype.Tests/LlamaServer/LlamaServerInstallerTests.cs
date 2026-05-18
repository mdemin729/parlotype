using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform.Speech;
using Parlotype.Platform.LlamaServer;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

public sealed class LlamaServerInstallerTests : IDisposable
{
    private readonly string _root;
    private readonly InMemorySettings _settings = new();
    private readonly JsonLlamaServerRegistry _registry;
    private readonly FixtureHandler _handler = new();
    private readonly StreamingFileDownloader _downloader;
    private readonly RecordingLifecycle _lifecycle = new();
    private DateTimeOffset _now = new(2026, 05, 17, 10, 00, 00, TimeSpan.Zero);

    private const string MainUrl = "https://example.test/llama-b9198-bin-win-cuda-12.4-x64.zip";
    private const string CompanionUrl = "https://example.test/cudart-llama-bin-win-cuda-12.4-x64.zip";
    private const string VulkanUrl = "https://example.test/llama-b9198-bin-win-vulkan-x64.zip";

    public LlamaServerInstallerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _registry = new JsonLlamaServerRegistry(
            _root, _settings, NullLogger<JsonLlamaServerRegistry>.Instance);
        _downloader = new StreamingFileDownloader(
            new HttpClient(_handler) { Timeout = TimeSpan.FromMinutes(1) },
            NullLogger<StreamingFileDownloader>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private LlamaServerInstaller NewInstaller(bool withLifecycle = true) => new(
        _root,
        _downloader,
        _registry,
        withLifecycle ? _lifecycle : null,
        NullLogger<LlamaServerInstaller>.Instance,
        () => _now);

    [Fact]
    public async Task BuildInstallId_StripsLlamaAndBin_PreservesCudaMinor()
    {
        var id = LlamaServerInstaller.BuildInstallId(VariantCuda(sha: null));
        Assert.Equal("b9198-win-cuda-12.4-x64", id);
    }

    [Fact]
    public async Task BuildInstallId_StripsTarGz()
    {
        var variant = new LlamaServerVariant(
            Build: "b9198",
            Backend: LlamaServerBackend.Metal,
            Os: LlamaServerOs.MacOs,
            Arch: LlamaServerArch.Arm64,
            AssetName: "llama-b9198-bin-macos-arm64.tar.gz",
            Bytes: 100,
            DownloadUrl: "https://example.test/macos.tar.gz",
            Sha256: null,
            CompanionAssetName: null,
            CompanionDownloadUrl: null,
            CompanionBytes: null,
            CompanionSha256: null);
        Assert.Equal("b9198-macos-arm64", LlamaServerInstaller.BuildInstallId(variant));
    }

    [Fact]
    public async Task Install_Vulkan_CreatesFolderAndManifestEntry()
    {
        var zip = BuildZip(("llama-server.exe", "VULKAN"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: Sha256(zip));

        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);

        Assert.Equal("b9198-win-vulkan-x64", install.Id);
        Assert.True(Directory.Exists(install.AbsolutePath));
        Assert.Equal("VULKAN",
            File.ReadAllText(Path.Combine(install.AbsolutePath, "llama-server.exe")));

        var manifestEntries = await _registry.ListManagedAsync();
        Assert.Single(manifestEntries);
        Assert.Equal(install.Id, manifestEntries[0].Id);
    }

    [Fact]
    public async Task Install_CudaWithCompanion_MergesBothArchives()
    {
        var mainZip = BuildZip(
            ("llama-server.exe", "MAIN"),
            ("ggml-cuda.dll", "CUDA"));
        var companionZip = BuildZip(
            ("cudart64_12.dll", "CUDART"));
        _handler.Set(MainUrl, mainZip);
        _handler.Set(CompanionUrl, companionZip);

        var variant = VariantCuda(sha: Sha256(mainZip), compSha: Sha256(companionZip));
        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);

        Assert.True(File.Exists(Path.Combine(install.AbsolutePath, "llama-server.exe")));
        Assert.True(File.Exists(Path.Combine(install.AbsolutePath, "ggml-cuda.dll")));
        Assert.True(File.Exists(Path.Combine(install.AbsolutePath, "cudart64_12.dll")));
        Assert.Equal("CUDART",
            File.ReadAllText(Path.Combine(install.AbsolutePath, "cudart64_12.dll")));
    }

    [Fact]
    public async Task Install_NoSha_LogsWarningAndSucceeds()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: null);

        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);
        Assert.True(File.Exists(Path.Combine(install.AbsolutePath, "llama-server.exe")));
    }

    [Fact]
    public async Task Install_ShaMismatch_ThrowsAndLeavesNoState()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var wrongSha = new string('a', 64);
        var variant = VariantVulkan(sha: wrongSha);

        var installer = NewInstaller();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.InstallAsync(variant, progress: null));

        Assert.False(Directory.Exists(Path.Combine(_root, "b9198-win-vulkan-x64")));
        Assert.Empty(await _registry.ListManagedAsync());
        AssertStagingIsClean();
    }

    [Fact]
    public async Task Install_CompanionShaMismatch_ThrowsAndLeavesNoState()
    {
        var mainZip = BuildZip(("llama-server.exe", "MAIN"));
        var companionZip = BuildZip(("cudart64_12.dll", "CUDART"));
        _handler.Set(MainUrl, mainZip);
        _handler.Set(CompanionUrl, companionZip);

        var variant = VariantCuda(sha: Sha256(mainZip), compSha: new string('f', 64));
        var installer = NewInstaller();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.InstallAsync(variant, progress: null));

        Assert.False(Directory.Exists(Path.Combine(_root, "b9198-win-cuda-12.4-x64")));
        Assert.Empty(await _registry.ListManagedAsync());
        AssertStagingIsClean();
    }

    [Fact]
    public async Task Install_DownloadHttpError_ThrowsAndLeavesNoState()
    {
        _handler.SetStatus(VulkanUrl, HttpStatusCode.NotFound);
        var variant = VariantVulkan(sha: null);

        var installer = NewInstaller();
        await Assert.ThrowsAsync<HttpRequestException>(
            () => installer.InstallAsync(variant, progress: null));

        Assert.Empty(await _registry.ListManagedAsync());
        AssertStagingIsClean();
    }

    [Fact]
    public async Task Install_Cancelled_LeavesNoInstallAndCleansStaging()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: Sha256(zip));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var installer = NewInstaller();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => installer.InstallAsync(variant, progress: null, cts.Token));

        Assert.Empty(await _registry.ListManagedAsync());
        AssertStagingIsClean();
    }

    [Fact]
    public async Task Install_MacOsVariant_ThrowsNotSupported()
    {
        var zip = BuildZip(("llama-server", "DATA"));
        var url = "https://example.test/llama-b9198-bin-macos-arm64.tar.gz";
        _handler.Set(url, zip);

        var variant = new LlamaServerVariant(
            Build: "b9198",
            Backend: LlamaServerBackend.Metal,
            Os: LlamaServerOs.MacOs,
            Arch: LlamaServerArch.Arm64,
            AssetName: "llama-b9198-bin-macos-arm64.tar.gz",
            Bytes: zip.Length,
            DownloadUrl: url,
            Sha256: null,
            CompanionAssetName: null,
            CompanionDownloadUrl: null,
            CompanionBytes: null,
            CompanionSha256: null);

        var installer = NewInstaller();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => installer.InstallAsync(variant, progress: null));
        AssertStagingIsClean();
    }

    [Fact]
    public async Task Install_OverExisting_ReplacesFolder()
    {
        var first = BuildZip(("llama-server.exe", "FIRST"));
        _handler.Set(VulkanUrl, first);
        var variant1 = VariantVulkan(sha: Sha256(first));

        var installer = NewInstaller();
        var install1 = await installer.InstallAsync(variant1, progress: null);
        Assert.Equal("FIRST",
            File.ReadAllText(Path.Combine(install1.AbsolutePath, "llama-server.exe")));

        var second = BuildZip(("llama-server.exe", "SECOND"));
        _handler.Reset();
        _handler.Set(VulkanUrl, second);
        var variant2 = VariantVulkan(sha: Sha256(second));

        var install2 = await installer.InstallAsync(variant2, progress: null);
        Assert.Equal(install1.Id, install2.Id);
        Assert.Equal("SECOND",
            File.ReadAllText(Path.Combine(install2.AbsolutePath, "llama-server.exe")));

        Assert.Single(await _registry.ListManagedAsync());
    }

    [Fact]
    public async Task Install_ReportsProgressForBothPhases()
    {
        var mainZip = BuildZip(("llama-server.exe", "MAIN"));
        var companionZip = BuildZip(("cudart64_12.dll", "CUDART"));
        _handler.Set(MainUrl, mainZip);
        _handler.Set(CompanionUrl, companionZip);

        var phases = new List<string>();
        var progress = new Progress<LlamaServerInstallProgress>(p => phases.Add(p.Phase));
        var variant = VariantCuda(sha: Sha256(mainZip), compSha: Sha256(companionZip));
        var installer = NewInstaller();
        await installer.InstallAsync(variant, progress);

        // Give Progress<T> callbacks a chance to drain (they post to current SynchronizationContext)
        await Task.Yield();

        Assert.Contains("downloading", phases);
        Assert.Contains("downloading-companion", phases);
        Assert.Contains("extracting", phases);
        Assert.Contains("finalizing", phases);
    }

    [Fact]
    public async Task Uninstall_RemovesFolderAndManifestEntry()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: Sha256(zip));

        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);
        Assert.True(Directory.Exists(install.AbsolutePath));

        await installer.UninstallAsync(install.Id);

        Assert.False(Directory.Exists(install.AbsolutePath));
        Assert.Empty(await _registry.ListManagedAsync());
    }

    [Fact]
    public async Task Uninstall_Active_StopsSidecarAndClearsActiveSelector()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: Sha256(zip));

        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);
        await _registry.SetActiveAsync(install.Id, LlamaServerSource.Managed);

        await installer.UninstallAsync(install.Id);

        Assert.Equal(1, _lifecycle.StopCount);
        Assert.Null(await _registry.GetActiveAsync());
    }

    [Fact]
    public async Task Uninstall_NotActive_DoesNotStopSidecar()
    {
        var zip = BuildZip(("llama-server.exe", "DATA"));
        _handler.Set(VulkanUrl, zip);
        var variant = VariantVulkan(sha: Sha256(zip));

        var installer = NewInstaller();
        var install = await installer.InstallAsync(variant, progress: null);
        // Never set active

        await installer.UninstallAsync(install.Id);

        Assert.Equal(0, _lifecycle.StopCount);
    }

    [Fact]
    public async Task Uninstall_UnknownId_IsNoOp()
    {
        var installer = NewInstaller();
        await installer.UninstallAsync("does-not-exist");
        Assert.Empty(await _registry.ListManagedAsync());
    }

    private void AssertStagingIsClean()
    {
        var stagingRoot = Path.Combine(_root, LlamaServerInstaller.StagingDirName);
        if (!Directory.Exists(stagingRoot)) return;
        Assert.Empty(Directory.GetDirectories(stagingRoot));
    }

    private static LlamaServerVariant VariantVulkan(string? sha) => new(
        Build: "b9198",
        Backend: LlamaServerBackend.Vulkan,
        Os: LlamaServerOs.Windows,
        Arch: LlamaServerArch.X64,
        AssetName: "llama-b9198-bin-win-vulkan-x64.zip",
        Bytes: 0,
        DownloadUrl: VulkanUrl,
        Sha256: sha,
        CompanionAssetName: null,
        CompanionDownloadUrl: null,
        CompanionBytes: null,
        CompanionSha256: null);

    private static LlamaServerVariant VariantCuda(string? sha, string? compSha = null) => new(
        Build: "b9198",
        Backend: LlamaServerBackend.Cuda12,
        Os: LlamaServerOs.Windows,
        Arch: LlamaServerArch.X64,
        AssetName: "llama-b9198-bin-win-cuda-12.4-x64.zip",
        Bytes: 0,
        DownloadUrl: MainUrl,
        Sha256: sha,
        CompanionAssetName: "cudart-llama-bin-win-cuda-12.4-x64.zip",
        CompanionDownloadUrl: CompanionUrl,
        CompanionBytes: 0,
        CompanionSha256: compSha);

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _bodies = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HttpStatusCode> _statuses = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string url, byte[] body) => _bodies[url] = body;
        public void SetStatus(string url, HttpStatusCode status) => _statuses[url] = status;

        public void Reset()
        {
            _bodies.Clear();
            _statuses.Clear();
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (_statuses.TryGetValue(url, out var status))
                return Task.FromResult(new HttpResponseMessage(status));
            if (_bodies.TryGetValue(url, out var body))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(body),
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class RecordingLifecycle : ILlamaCppServerLifecycle
    {
        public int StopCount { get; private set; }

        public Task StopForReplacementAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var raw) && raw is T typed)
                return Task.FromResult<T?>(typed);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }
}
