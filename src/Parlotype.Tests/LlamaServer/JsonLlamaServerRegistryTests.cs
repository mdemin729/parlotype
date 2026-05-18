using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform.LlamaServer;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

public sealed class JsonLlamaServerRegistryTests : IDisposable
{
    private readonly string _root;
    private readonly InMemorySettings _settings = new();

    public JsonLlamaServerRegistryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private JsonLlamaServerRegistry NewRegistry() =>
        new(_root, _settings, NullLogger<JsonLlamaServerRegistry>.Instance);

    private static LlamaServerManagedInstallRecord SampleRecord(
        string id = "b9198-win-cuda-12.4-x64",
        string build = "b9198",
        LlamaServerBackend backend = LlamaServerBackend.Cuda12,
        DateTimeOffset? installedAt = null) =>
        new(
            Id: id,
            Build: build,
            Backend: backend,
            Os: LlamaServerOs.Windows,
            Arch: LlamaServerArch.X64,
            AssetName: $"llama-{build}-bin-win-cuda-12.4-x64.zip",
            CompanionAssetName: "cudart-llama-bin-win-cuda-12.4-x64.zip",
            Sha256: "8c79a9b226de4b3ca",
            CompanionSha256: "f96935e7e385e3b2d",
            InstalledAt: installedAt ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task ListManagedAsync_EmptyRoot_ReturnsEmpty()
    {
        var registry = NewRegistry();
        var installs = await registry.ListManagedAsync();
        Assert.Empty(installs);
    }

    [Fact]
    public async Task AddOrUpdate_ThenList_RoundTripsEntry()
    {
        var registry = NewRegistry();
        var record = SampleRecord();
        await registry.AddOrUpdateAsync(record);

        var installs = await registry.ListManagedAsync();
        var install = Assert.Single(installs);

        Assert.Equal(record.Id, install.Id);
        Assert.Equal(LlamaServerSource.Managed, install.Source);
        Assert.Equal(record.Build, install.Build);
        Assert.Equal(LlamaServerBackend.Cuda12, install.Backend);
        Assert.Equal(Path.Combine(_root, record.Id), install.AbsolutePath);
        Assert.False(install.IsValid, "no install folder exists yet, so IsValid should be false");
    }

    [Fact]
    public async Task IsValid_TrueWhenInstallFolderExists()
    {
        var registry = NewRegistry();
        var record = SampleRecord();
        Directory.CreateDirectory(Path.Combine(_root, record.Id));
        await registry.AddOrUpdateAsync(record);

        var installs = await registry.ListManagedAsync();
        Assert.True(installs.Single().IsValid);
    }

    [Fact]
    public async Task AddOrUpdate_SameId_ReplacesExisting()
    {
        var registry = NewRegistry();
        var first = SampleRecord(installedAt: DateTimeOffset.UtcNow.AddDays(-1));
        await registry.AddOrUpdateAsync(first);

        var updated = first with { InstalledAt = DateTimeOffset.UtcNow, Sha256 = "deadbeef" };
        await registry.AddOrUpdateAsync(updated);

        var installs = await registry.ListManagedAsync();
        var only = Assert.Single(installs);
        Assert.Equal(updated.InstalledAt, only.InstalledAt);
    }

    [Fact]
    public async Task ListManagedAsync_ReturnsNewestFirst()
    {
        var registry = NewRegistry();
        var older = SampleRecord(id: "b9000-win-vulkan-x64",
            backend: LlamaServerBackend.Vulkan,
            installedAt: DateTimeOffset.UtcNow.AddDays(-7));
        var newer = SampleRecord(id: "b9198-win-cuda-12.4-x64",
            backend: LlamaServerBackend.Cuda12,
            installedAt: DateTimeOffset.UtcNow);
        await registry.AddOrUpdateAsync(older);
        await registry.AddOrUpdateAsync(newer);

        var installs = await registry.ListManagedAsync();
        Assert.Collection(installs,
            i => Assert.Equal(newer.Id, i.Id),
            i => Assert.Equal(older.Id, i.Id));
    }

    [Fact]
    public async Task GetManagedAsync_KnownId_ReturnsInstall()
    {
        var registry = NewRegistry();
        var record = SampleRecord();
        await registry.AddOrUpdateAsync(record);

        var install = await registry.GetManagedAsync(record.Id);
        Assert.NotNull(install);
        Assert.Equal(record.Id, install!.Id);
    }

    [Fact]
    public async Task GetManagedAsync_UnknownId_ReturnsNull()
    {
        var registry = NewRegistry();
        var install = await registry.GetManagedAsync("does-not-exist");
        Assert.Null(install);
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntry()
    {
        var registry = NewRegistry();
        var record = SampleRecord();
        await registry.AddOrUpdateAsync(record);

        await registry.RemoveAsync(record.Id);

        Assert.Empty(await registry.ListManagedAsync());
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_IsNoOp()
    {
        var registry = NewRegistry();
        await registry.RemoveAsync("does-not-exist");
        Assert.Empty(await registry.ListManagedAsync());
    }

    [Fact]
    public async Task GetActiveAsync_NoSelector_ReturnsNull()
    {
        var registry = NewRegistry();
        Assert.Null(await registry.GetActiveAsync());
    }

    [Fact]
    public async Task SetActive_Managed_ThenGetActive_ReturnsManagedInstall()
    {
        var registry = NewRegistry();
        var record = SampleRecord();
        await registry.AddOrUpdateAsync(record);

        await registry.SetActiveAsync(record.Id, LlamaServerSource.Managed);

        var active = await registry.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(record.Id, active!.Id);
        Assert.Equal(LlamaServerSource.Managed, active.Source);
    }

    [Fact]
    public async Task SetActive_ManagedMissingFromManifest_GetActiveReturnsNull()
    {
        var registry = NewRegistry();
        await registry.SetActiveAsync("ghost-id", LlamaServerSource.Managed);

        var active = await registry.GetActiveAsync();
        Assert.Null(active);
    }

    [Fact]
    public async Task SetActive_Manual_ReturnsManualInstallWithSettingsFolder()
    {
        var registry = NewRegistry();
        var manualFolder = Path.Combine(_root, "user-pick");
        Directory.CreateDirectory(manualFolder);
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, manualFolder);

        await registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);

        var active = await registry.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(LlamaServerSource.Manual, active!.Source);
        Assert.Equal(manualFolder, active.AbsolutePath);
        Assert.True(active.IsValid);
    }

    [Fact]
    public async Task SetActive_ManualWithNoFolderSetting_GetActiveReturnsNull()
    {
        var registry = NewRegistry();
        await registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);
        Assert.Null(await registry.GetActiveAsync());
    }

    [Fact]
    public async Task SetActive_Manual_RejectsInstallId()
    {
        var registry = NewRegistry();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            registry.SetActiveAsync("some-id", LlamaServerSource.Manual));
    }

    [Fact]
    public async Task SetActive_ManagedWithNullId_ClearsSelector()
    {
        var registry = NewRegistry();
        await registry.SetActiveAsync("anything", LlamaServerSource.Managed);
        await registry.SetActiveAsync(installId: null, LlamaServerSource.Managed);

        Assert.Null(await registry.GetActiveAsync());
    }

    [Fact]
    public async Task LoadManifest_MissingFile_StartsFreshNoError()
    {
        // The manifest file is never created until we add an entry.
        var manifestPath = Path.Combine(_root, JsonLlamaServerRegistry.ManifestFileName);
        Assert.False(File.Exists(manifestPath));

        var registry = NewRegistry();
        Assert.Empty(await registry.ListManagedAsync());
        Assert.False(File.Exists(manifestPath));
    }

    [Fact]
    public async Task LoadManifest_CorruptFile_QuarantinesAndStartsFresh()
    {
        var manifestPath = Path.Combine(_root, JsonLlamaServerRegistry.ManifestFileName);
        await File.WriteAllTextAsync(manifestPath, "{ this is not valid json :::");

        var registry = NewRegistry();
        var installs = await registry.ListManagedAsync();

        Assert.Empty(installs);

        // Original file moved aside, a .bak.* exists, original is gone.
        Assert.False(File.Exists(manifestPath));
        var backups = Directory.GetFiles(_root,
            JsonLlamaServerRegistry.ManifestFileName + ".bak.*");
        Assert.Single(backups);
    }

    [Fact]
    public async Task LoadManifest_UnknownBackend_DecodesAsUnknown()
    {
        var manifestPath = Path.Combine(_root, JsonLlamaServerRegistry.ManifestFileName);
        var json = """
            {
              "version": 1,
              "installs": [
                {
                  "id": "b9999-win-warpdrive-x64",
                  "build": "b9999",
                  "backend": "WarpDrive",
                  "os": "Windows",
                  "arch": "X64",
                  "assetName": "llama-b9999-bin-win-warpdrive-x64.zip",
                  "installedAt": "2026-05-17T00:00:00+00:00"
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(manifestPath, json);

        var registry = NewRegistry();
        var install = (await registry.ListManagedAsync()).Single();
        Assert.Equal(LlamaServerBackend.Unknown, install.Backend);
    }

    [Fact]
    public async Task RegistrySurvivesRoundTripAcrossInstances()
    {
        var first = NewRegistry();
        await first.AddOrUpdateAsync(SampleRecord());

        var second = NewRegistry();
        var install = Assert.Single(await second.ListManagedAsync());
        Assert.Equal("b9198-win-cuda-12.4-x64", install.Id);
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
