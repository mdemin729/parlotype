using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests.Speech;

public sealed class JsonPromptTemplateRegistryTests : IDisposable
{
    private readonly string _dir;
    private readonly InMemorySettings _settings = new();

    public JsonPromptTemplateRegistryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private JsonPromptTemplateRegistry NewRegistry() =>
        new(_dir, _settings, NullLogger<JsonPromptTemplateRegistry>.Instance);

    private static PromptTemplate UserPrompt(string id = "u1", string name = "Custom", string text = "Do {language}.") =>
        new(id, name, text);

    [Fact]
    public async Task ListAsync_EmptyDir_ReturnsOnlyBuiltIn()
    {
        var registry = NewRegistry();
        var prompts = await registry.ListAsync();

        var only = Assert.Single(prompts);
        Assert.True(only.IsBuiltIn);
        Assert.Equal(JsonPromptTemplateRegistry.BuiltInDefaultId, only.Id);
    }

    [Fact]
    public async Task AddOrUpdate_ThenList_RoundTripsUserPrompt()
    {
        var registry = NewRegistry();
        await registry.AddOrUpdateAsync(UserPrompt());

        var prompts = await registry.ListAsync();
        Assert.Equal(2, prompts.Count);
        Assert.True(prompts[0].IsBuiltIn);
        Assert.Equal("u1", prompts[1].Id);
        Assert.False(prompts[1].IsBuiltIn);
    }

    [Fact]
    public async Task UserPrompts_PersistAcrossRegistryInstances()
    {
        await NewRegistry().AddOrUpdateAsync(UserPrompt(name: "Persisted"));

        var reloaded = await NewRegistry().GetAsync("u1");
        Assert.NotNull(reloaded);
        Assert.Equal("Persisted", reloaded!.Name);
    }

    [Fact]
    public async Task AddOrUpdate_BuiltIn_Throws()
    {
        var registry = NewRegistry();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.AddOrUpdateAsync(JsonPromptTemplateRegistry.BuiltInDefault));
    }

    [Fact]
    public async Task Remove_BuiltIn_Throws()
    {
        var registry = NewRegistry();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.RemoveAsync(JsonPromptTemplateRegistry.BuiltInDefaultId));
    }

    [Fact]
    public async Task Remove_UserPrompt_DeletesIt()
    {
        var registry = NewRegistry();
        await registry.AddOrUpdateAsync(UserPrompt());
        await registry.RemoveAsync("u1");

        Assert.Null(await registry.GetAsync("u1"));
    }

    [Fact]
    public async Task SetActive_ThenGetActive_ReturnsSelection()
    {
        var registry = NewRegistry();
        await registry.AddOrUpdateAsync(UserPrompt());
        await registry.SetActiveAsync("u1");

        var active = await registry.GetActiveAsync();
        Assert.Equal("u1", active.Id);
    }

    [Fact]
    public async Task GetActive_NoSelection_FallsBackToBuiltIn()
    {
        var registry = NewRegistry();
        var active = await registry.GetActiveAsync();
        Assert.Equal(JsonPromptTemplateRegistry.BuiltInDefaultId, active.Id);
    }

    [Fact]
    public async Task RemovingActivePrompt_ResetsActiveToBuiltIn()
    {
        var registry = NewRegistry();
        await registry.AddOrUpdateAsync(UserPrompt());
        await registry.SetActiveAsync("u1");
        await registry.RemoveAsync("u1");

        var active = await registry.GetActiveAsync();
        Assert.Equal(JsonPromptTemplateRegistry.BuiltInDefaultId, active.Id);
    }

    [Fact]
    public void Render_SubstitutesLanguageToken()
    {
        var prompt = new PromptTemplate("x", "x", "Transcribe {language} into {language}.");
        Assert.Equal("Transcribe French into French.", prompt.Render("French"));
    }

    [Fact]
    public void Render_NoLanguage_UsesDefault()
    {
        var prompt = new PromptTemplate("x", "x", "Speak {language}.");
        Assert.Equal($"Speak {PromptTemplate.DefaultLanguage}.", prompt.Render());
    }

    [Fact]
    public async Task CorruptFile_IsQuarantined_AndBuiltInStillReturned()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_dir, JsonPromptTemplateRegistry.FileName),
            "{ this is not valid json");

        var registry = NewRegistry();
        var prompts = await registry.ListAsync();

        var only = Assert.Single(prompts);
        Assert.True(only.IsBuiltIn);
        Assert.Contains(
            Directory.GetFiles(_dir),
            f => f.Contains(JsonPromptTemplateRegistry.FileName + ".bak", StringComparison.Ordinal));
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
