using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform.Speech;
using Parlotype.Platform.LlamaServer;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

/// <summary>
/// Covers the active-install path-resolution rules in
/// <see cref="LlamaCppSpeechRecognizer.GetServerPathAsync"/>: managed wins,
/// manual falls through to the legacy folder setting, and a stale managed
/// selector falls back gracefully without throwing.
/// </summary>
public sealed class LlamaCppSpeechRecognizerPathResolutionTests : IDisposable
{
    private readonly string _root;
    private readonly InMemorySettings _settings = new();
    private readonly JsonLlamaServerRegistry _registry;

    public LlamaCppSpeechRecognizerPathResolutionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _registry = new JsonLlamaServerRegistry(
            _root, _settings, NullLogger<JsonLlamaServerRegistry>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private LlamaCppSpeechRecognizer NewRecognizer() =>
        new(_settings, _registry, NullLogger<LlamaCppSpeechRecognizer>.Instance);

    private static LlamaServerManagedInstallRecord SampleRecord(string id) => new(
        Id: id,
        Build: "b9198",
        Backend: LlamaServerBackend.Vulkan,
        Os: LlamaServerOs.Windows,
        Arch: LlamaServerArch.X64,
        AssetName: "llama-b9198-bin-win-vulkan-x64.zip",
        CompanionAssetName: null,
        Sha256: null,
        CompanionSha256: null,
        InstalledAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task ManagedActive_ResolvesToManagedFolder()
    {
        await _registry.AddOrUpdateAsync(SampleRecord("b9198-win-vulkan-x64"));
        await _registry.SetActiveAsync("b9198-win-vulkan-x64", LlamaServerSource.Managed);

        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        Assert.Equal(
            Path.Combine(_root, "b9198-win-vulkan-x64", "llama-server.exe"),
            path);
    }

    [Fact]
    public async Task ManualActive_ResolvesToLlamaCppServerFolderSetting()
    {
        var manualFolder = Path.Combine(_root, "my-llama");
        Directory.CreateDirectory(manualFolder);
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, manualFolder);
        await _registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);

        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(manualFolder, "llama-server.exe"), path);
    }

    [Fact]
    public async Task NoActiveSelector_WithFolderSetting_UsesFolder()
    {
        var manualFolder = Path.Combine(_root, "legacy");
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, manualFolder);

        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(manualFolder, "llama-server.exe"), path);
    }

    [Fact]
    public async Task NoActiveSelector_NoFolderSetting_UsesDefaultLegacyFolder()
    {
        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "llama-server");
        Assert.Equal(Path.Combine(defaultFolder, "llama-server.exe"), path);
    }

    [Fact]
    public async Task StaleManagedSelector_NotInManifest_FallsBackToLegacyFolder()
    {
        // Set a managed selector pointing at an id that no longer exists,
        // and a legacy folder so we can detect the fallback path.
        var manualFolder = Path.Combine(_root, "fallback");
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, manualFolder);
        await _settings.SetAsync(
            SettingsKeys.LlamaCppActiveInstall, "managed:ghost-id");

        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(manualFolder, "llama-server.exe"), path);
    }

    [Fact]
    public async Task ManualActive_NoFolderSetting_UsesDefaultLegacyFolder()
    {
        await _registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);

        var recognizer = NewRecognizer();
        var path = await recognizer.GetServerPathAsync(CancellationToken.None);

        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "llama-server");
        Assert.Equal(Path.Combine(defaultFolder, "llama-server.exe"), path);
    }

    [Fact]
    public async Task StopForReplacementAsync_WhenNotRunning_DoesNotThrow()
    {
        var recognizer = NewRecognizer();
        // No server running; should be a clean no-op.
        await recognizer.StopForReplacementAsync();
        Assert.False(recognizer.IsReady);
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
