using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform;
using Parlotype.Platform.LlamaServer;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

/// <summary>
/// Smoke test for the Platform DI registration of llama-server services.
/// Catches regressions where someone adds a new constructor parameter or
/// removes a registration without re-running the desktop app.
/// </summary>
public sealed class LlamaServerDiCompositionTests
{
    private static ServiceProvider BuildPlatformProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddPlatformServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Registry_Resolves()
    {
        var provider = BuildPlatformProvider();
        await using var _ = provider;
        Assert.NotNull(provider.GetRequiredService<ILlamaServerRegistry>());
    }

    [Fact]
    public async Task Catalog_Resolves()
    {
        var provider = BuildPlatformProvider();
        await using var _ = provider;
        Assert.NotNull(provider.GetRequiredService<ILlamaServerCatalog>());
    }

    [Fact]
    public async Task Installer_Resolves_WithLifecycleInjected()
    {
        var provider = BuildPlatformProvider();
        await using var _ = provider;
        Assert.NotNull(provider.GetRequiredService<ILlamaServerInstaller>());
    }

    /// <summary>
    /// Both consumers run their own llama-server from the same installed binary
    /// (ADR-073), so the lifecycle the installer gets must stop both, and must
    /// reach the same instances the app actually uses.
    /// </summary>
    [Fact]
    public async Task Lifecycle_StopsBothTheRecognizerAndSpeechCleanup()
    {
        var provider = BuildPlatformProvider();
        await using var _ = provider;
        var recognizer = provider.GetRequiredService<LlamaCppSpeechRecognizer>();
        var cleanup = provider.GetRequiredService<ISpeechCleanupService>();

        var lifecycle = Assert.IsType<CompositeLlamaServerLifecycle>(
            provider.GetRequiredService<ILlamaCppServerLifecycle>());

        Assert.Equal(2, lifecycle.Members.Count);
        Assert.Contains(lifecycle.Members, m => ReferenceEquals(m, recognizer));
        Assert.Contains(lifecycle.Members, m => ReferenceEquals(m, cleanup));
    }
}
