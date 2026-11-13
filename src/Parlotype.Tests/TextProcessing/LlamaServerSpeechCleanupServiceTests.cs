using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform.LlamaServer;
using Parlotype.Platform.Speech;
using Parlotype.Platform.TextProcessing;
using Xunit;

namespace Parlotype.Tests.TextProcessing;

/// <summary>
/// The parts of <see cref="LlamaServerSpeechCleanupService"/> that need no
/// running server: readiness from what is on disk, the command line, the wire
/// format, and refusing to start without its prerequisites.
/// </summary>
public sealed class LlamaServerSpeechCleanupServiceTests : IDisposable
{
    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(key, out var raw) && raw is T typed ? typed : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class TempAppPaths(string root) : IAppPaths
    {
        public string DataDirectory => root;
        public string SettingsDirectory => root;
        public string ModelsDirectory => Path.Combine(root, "models");
        public string LogsDirectory => Path.Combine(root, "logs");
        public string LlamaServerInstallsDirectory => Path.Combine(root, "llama-servers");
        public string SettingsFilePath => Path.Combine(root, "settings.json");
        public string SecretsFilePath => Path.Combine(root, "secrets.json");
        public string WindowStateFilePath => Path.Combine(root, "window-state.json");
    }

    private readonly string _root;
    private readonly InMemorySettings _settings = new();
    private readonly TempAppPaths _paths;
    private readonly JsonLlamaServerRegistry _registry;
    private readonly SpeechCleanupModelDownloadService _models;
    private readonly LlamaServerSpeechCleanupService _service;

    public LlamaServerSpeechCleanupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _paths = new TempAppPaths(_root);
        _registry = new JsonLlamaServerRegistry(
            _paths.LlamaServerInstallsDirectory, _settings, NullLogger<JsonLlamaServerRegistry>.Instance);
        _models = new SpeechCleanupModelDownloadService(
            new StreamingFileDownloader(new HttpClient(), NullLogger<StreamingFileDownloader>.Instance),
            _paths,
            NullLogger<SpeechCleanupModelDownloadService>.Instance);
        _service = new LlamaServerSpeechCleanupService(
            _settings, _registry, _models, NullLogger<LlamaServerSpeechCleanupService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void PlaceModelFile()
    {
        Directory.CreateDirectory(_paths.ModelsDirectory);
        File.WriteAllText(_models.GetModelPath(SpeechCleanupModelInfo.Default), "not a real model");
    }

    private async Task PlaceManualServerAsync()
    {
        var folder = Path.Combine(_root, "manual-server");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "llama-server.exe"), "not a real executable");
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, folder);
        await _registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);
    }

    // --- Readiness ---

    [Fact]
    public async Task Readiness_NothingOnDisk_ReportsTheModelFirst() =>
        Assert.Equal(SpeechCleanupReadiness.ModelMissing, await _service.GetReadinessAsync());

    [Fact]
    public async Task Readiness_ModelButNoServer_ReportsTheServer()
    {
        PlaceModelFile();

        Assert.Equal(SpeechCleanupReadiness.ServerMissing, await _service.GetReadinessAsync());
    }

    [Fact]
    public async Task Readiness_ServerFolderWithoutTheExecutable_ReportsTheServer()
    {
        PlaceModelFile();
        var folder = Path.Combine(_root, "empty-server");
        Directory.CreateDirectory(folder);
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, folder);

        Assert.Equal(SpeechCleanupReadiness.ServerMissing, await _service.GetReadinessAsync());
    }

    [Fact]
    public async Task Readiness_ModelAndServer_IsReady()
    {
        PlaceModelFile();
        await PlaceManualServerAsync();

        Assert.Equal(SpeechCleanupReadiness.Ready, await _service.GetReadinessAsync());
    }

    // --- Refusing to start ---

    [Fact]
    public async Task Clean_WithoutTheModel_ThrowsInsteadOfStartingAnything()
    {
        await PlaceManualServerAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CleanAsync("hello there", SpeechCleanupOptions.Default));
        Assert.Contains("model not found", ex.Message);
    }

    [Fact]
    public async Task Prepare_WithoutAServer_Throws()
    {
        PlaceModelFile();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.PrepareAsync());
        Assert.Contains("No llama-server", ex.Message);
    }

    [Fact]
    public async Task Unload_WhenNothingIsRunning_DoesNotThrow()
    {
        await _service.UnloadAsync();
        await _service.StopForReplacementAsync();
    }

    // --- Command line ---

    [Fact]
    public void BuildArguments_DisablesThinkingThroughTheTemplate_NotTheReasoningBudget()
    {
        var args = LlamaServerSpeechCleanupService.BuildArguments(@"C:\models\s1.gguf", 51234);

        Assert.Contains("--jinja", args);
        var kwargs = args[args.ToList().IndexOf("--chat-template-kwargs") + 1];
        using var doc = JsonDocument.Parse(kwargs);
        Assert.False(doc.RootElement.GetProperty("enable_thinking").GetBoolean());
        Assert.DoesNotContain("--reasoning-budget", args);
    }

    [Fact]
    public void BuildArguments_PinsGreedyDecodingOneSlotAndLoopback()
    {
        var args = LlamaServerSpeechCleanupService.BuildArguments(@"C:\models\s1.gguf", 51234).ToList();

        Assert.Equal(@"C:\models\s1.gguf", args[args.IndexOf("-m") + 1]);
        Assert.Equal("127.0.0.1", args[args.IndexOf("--host") + 1]);
        Assert.Equal("51234", args[args.IndexOf("--port") + 1]);
        Assert.Equal("0", args[args.IndexOf("--temp") + 1]);
        Assert.Equal("1", args[args.IndexOf("-np") + 1]);
        Assert.Equal(
            SpeechCleanupModelInfo.Default.ContextTokens.ToString(),
            args[args.IndexOf("-c") + 1]);
    }

    // --- Wire format ---

    [Fact]
    public void Request_CarriesTheSystemPromptTheControlLineAndGreedySettings()
    {
        var body = SpeechCleanupRequest.Build(SpeechCleanupOptions.Default, "um hello there");
        using var doc = JsonDocument.Parse(body.ToJsonString());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.False(root.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(S1MiniPrompt.SystemPrompt, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(
            "[Styling: semi-formal] [Structure: prose] [Context: general]\num hello there",
            messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void Request_BoundsTheOutputByTheInputLength()
    {
        var shortBody = SpeechCleanupRequest.Build(SpeechCleanupOptions.Default, "hi");
        var longBody = SpeechCleanupRequest.Build(SpeechCleanupOptions.Default, new string('a', 2000));

        Assert.Equal(65, shortBody["max_tokens"]!.GetValue<int>());
        Assert.Equal(1064, longBody["max_tokens"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("""{"choices":[{"message":{"content":"  I need the report by Thursday.\n"}}]}""", "I need the report by Thursday.")]
    [InlineData("""{"choices":[{"message":{"content":""}}]}""", "")]
    [InlineData("""{"choices":[{"message":{"content":null}}]}""", "")]
    public void ParseContent_ReturnsTheTrimmedText_AndTreatsEmptyAsValid(string json, string expected)
    {
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(expected, SpeechCleanupRequest.ParseContent(doc.RootElement));
    }

    // --- Lifecycle composite ---

    private sealed class RecordingLifecycle(List<string> log, string name) : ILlamaCppServerLifecycle
    {
        public Task StopForReplacementAsync(CancellationToken cancellationToken = default)
        {
            log.Add(name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CompositeLifecycle_StopsEveryConsumer()
    {
        var log = new List<string>();
        var composite = new CompositeLlamaServerLifecycle(
            new RecordingLifecycle(log, "recognizer"), new RecordingLifecycle(log, "cleanup"));

        await composite.StopForReplacementAsync();

        Assert.Equal(["recognizer", "cleanup"], log);
    }
}
