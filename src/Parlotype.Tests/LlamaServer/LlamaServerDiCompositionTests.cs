using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform;
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

    [Fact]
    public async Task LifecycleAndRecognizer_ResolveToSameInstance()
    {
        var provider = BuildPlatformProvider();
        await using var _ = provider;
        var recognizer = provider.GetRequiredService<LlamaCppSpeechRecognizer>();
        var lifecycle = provider.GetRequiredService<ILlamaCppServerLifecycle>();
        Assert.Same(recognizer, lifecycle);
    }
}
