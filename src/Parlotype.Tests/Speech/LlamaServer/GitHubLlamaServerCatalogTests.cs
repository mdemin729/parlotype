using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech.LlamaServer;
using Parlotype.Platform.Speech.LlamaServer;
using Xunit;

namespace Parlotype.Tests.Speech.LlamaServer;

public sealed class GitHubLlamaServerCatalogTests : IDisposable
{
    private readonly string _root;
    private readonly string _fixtureBody;
    private DateTimeOffset _now = new(2026, 05, 17, 10, 00, 00, TimeSpan.Zero);

    public GitHubLlamaServerCatalogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "llama-cpp-releases.json");
        _fixtureBody = File.ReadAllText(fixturePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private GitHubLlamaServerCatalog NewCatalog(
        ScriptedHandler handler,
        LlamaServerOs os = LlamaServerOs.Windows,
        LlamaServerArch arch = LlamaServerArch.X64,
        TimeSpan? ttl = null) =>
        new(
            new HttpClient(handler),
            _root,
            NullLogger<GitHubLlamaServerCatalog>.Instance,
            () => _now,
            os,
            arch,
            ttl ?? TimeSpan.FromHours(1));

    [Fact]
    public async Task FirstFetch_NoCache_HitsHttp_AndReturnsFilteredVariants()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody, etag: "W/\"v1\"");

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        Assert.Single(handler.Requests);
        Assert.Equal(2, groups.Count); // b9198 + b9197; draft + prerelease excluded
        Assert.Equal("b9198", groups[0].Build);
        Assert.Equal("b9197", groups[1].Build);
    }

    [Fact]
    public async Task FirstFetch_WindowsX64_FiltersOutOtherPlatforms()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var b9198 = groups.Single(g => g.Build == "b9198");
        var names = b9198.Variants.Select(v => v.AssetName).ToHashSet();

        // Windows x64 variants present
        Assert.Contains("llama-b9198-bin-win-cuda-12.4-x64.zip", names);
        Assert.Contains("llama-b9198-bin-win-cuda-13.1-x64.zip", names);
        Assert.Contains("llama-b9198-bin-win-vulkan-x64.zip", names);
        Assert.Contains("llama-b9198-bin-win-cpu-x64.zip", names);
        Assert.Contains("llama-b9198-bin-win-hip-radeon-x64.zip", names);

        // Filtered out (wrong os/arch or companion/unknown)
        Assert.DoesNotContain("llama-b9198-bin-win-cpu-arm64.zip", names);
        Assert.DoesNotContain("llama-b9198-bin-macos-arm64.tar.gz", names);
        Assert.DoesNotContain("llama-b9198-bin-ubuntu-vulkan-x64.tar.gz", names);
        Assert.DoesNotContain("llama-b9198-bin-android-arm64.tar.gz", names);
        Assert.DoesNotContain("llama-b9198-bin-310p-openEuler-x86.tar.gz", names);
        Assert.DoesNotContain("cudart-llama-bin-win-cuda-12.4-x64.zip", names);
        Assert.DoesNotContain("cudart-llama-bin-win-cuda-13.1-x64.zip", names);
        Assert.DoesNotContain("Source code (zip)", names);
    }

    [Fact]
    public async Task CudaVariants_PairedWithMatchingCudartCompanion()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var b9198 = groups.Single(g => g.Build == "b9198");
        var cuda12 = b9198.Variants.Single(v => v.AssetName.Contains("cuda-12.4"));
        Assert.Equal(LlamaServerBackend.Cuda12, cuda12.Backend);
        Assert.Equal("cudart-llama-bin-win-cuda-12.4-x64.zip", cuda12.CompanionAssetName);
        Assert.Equal(373_000_000, cuda12.CompanionBytes);
        Assert.StartsWith("ccccccc", cuda12.CompanionSha256);
        Assert.DoesNotContain("sha256:", cuda12.CompanionSha256);

        var cuda13 = b9198.Variants.Single(v => v.AssetName.Contains("cuda-13.1"));
        Assert.Equal(LlamaServerBackend.Cuda13, cuda13.Backend);
        Assert.Equal("cudart-llama-bin-win-cuda-13.1-x64.zip", cuda13.CompanionAssetName);
    }

    [Fact]
    public async Task NonCudaVariants_HaveNoCompanion()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);
        var b9198 = groups.Single(g => g.Build == "b9198");
        var vulkan = b9198.Variants.Single(v => v.Backend == LlamaServerBackend.Vulkan);

        Assert.Null(vulkan.CompanionAssetName);
        Assert.Null(vulkan.CompanionDownloadUrl);
        Assert.Null(vulkan.CompanionBytes);
        Assert.Null(vulkan.CompanionSha256);
    }

    [Fact]
    public async Task Sha256Digest_PrefixStripped()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);
        var variant = groups.Single(g => g.Build == "b9198").Variants
            .Single(v => v.Backend == LlamaServerBackend.Vulkan);

        Assert.NotNull(variant.Sha256);
        Assert.DoesNotContain("sha256:", variant.Sha256);
        Assert.Equal(64, variant.Sha256!.Length); // 256-bit hex
    }

    [Fact]
    public async Task NullDigest_SurfacedAsNull()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);
        var hip = groups.Single(g => g.Build == "b9198").Variants
            .Single(v => v.Backend == LlamaServerBackend.Hip);
        Assert.Null(hip.Sha256);
    }

    [Fact]
    public async Task CacheFresh_SecondFetchHitsCache_NoHttpCall()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody, etag: "W/\"v1\"");

        var catalog = NewCatalog(handler);
        var first = await catalog.FetchAsync(forceRefresh: false);

        // Within TTL
        _now = _now.AddMinutes(30);
        var second = await catalog.FetchAsync(forceRefresh: false);

        Assert.Single(handler.Requests);
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public async Task CacheStale_SecondFetchSendsIfNoneMatch_304ExtendsCache()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody, etag: "W/\"v1\"");
        handler.Enqueue(req =>
        {
            Assert.Contains(req.Headers.TryGetValues("If-None-Match", out var values) ? values : Array.Empty<string>(),
                v => v == "W/\"v1\"");
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });

        var catalog = NewCatalog(handler);
        await catalog.FetchAsync(forceRefresh: false);

        // Past TTL
        _now = _now.AddHours(2);
        var refreshed = await catalog.FetchAsync(forceRefresh: false);

        Assert.Equal(2, handler.Requests.Count);
        Assert.NotEmpty(refreshed);
    }

    [Fact]
    public async Task ForceRefresh_BypassesTtl()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody, etag: "W/\"v1\"");
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotModified));

        var catalog = NewCatalog(handler);
        await catalog.FetchAsync(forceRefresh: false);
        await catalog.FetchAsync(forceRefresh: true);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task HttpFailure_WithCache_FallsBackToCache()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);
        handler.Enqueue(_ => throw new HttpRequestException("simulated network drop"));

        var catalog = NewCatalog(handler);
        await catalog.FetchAsync(forceRefresh: false);

        _now = _now.AddHours(2);
        var fallback = await catalog.FetchAsync(forceRefresh: false);

        Assert.NotEmpty(fallback);
    }

    [Fact]
    public async Task HttpFailure_NoCache_Throws()
    {
        var handler = new ScriptedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("simulated network drop"));

        var catalog = NewCatalog(handler);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => catalog.FetchAsync(forceRefresh: false));
    }

    [Fact]
    public async Task Http500_WithCache_FallsBackToCache()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var catalog = NewCatalog(handler);
        await catalog.FetchAsync(forceRefresh: false);

        _now = _now.AddHours(2);
        var fallback = await catalog.FetchAsync(forceRefresh: false);
        Assert.NotEmpty(fallback);
    }

    [Fact]
    public async Task UserAgent_AndAcceptHeaders_Sent()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        await catalog.FetchAsync(forceRefresh: false);

        var req = handler.Requests.Single();
        Assert.Contains(req.Headers.UserAgent, ua => ua.Product?.Name == "parlotype");
        Assert.Contains(req.Headers.Accept, a => a.MediaType == "application/vnd.github+json");
    }

    [Fact]
    public async Task CachePersistsAcrossCatalogInstances()
    {
        var handler1 = new ScriptedHandler();
        handler1.EnqueueOk(_fixtureBody);
        var first = NewCatalog(handler1);
        await first.FetchAsync(forceRefresh: false);

        var handler2 = new ScriptedHandler(); // second instance: zero scripted responses
        var second = NewCatalog(handler2);
        var groups = await second.FetchAsync(forceRefresh: false);

        Assert.Empty(handler2.Requests);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public async Task FilterToMacOsArm64_ReturnsOnlyMacOs()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler, LlamaServerOs.MacOs, LlamaServerArch.Arm64);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var b9198 = groups.Single(g => g.Build == "b9198");
        var variant = Assert.Single(b9198.Variants);
        Assert.Equal("llama-b9198-bin-macos-arm64.tar.gz", variant.AssetName);
        Assert.Equal(LlamaServerOs.MacOs, variant.Os);
    }

    [Fact]
    public async Task FilterToLinuxX64_ReturnsOnlyUbuntu()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler, LlamaServerOs.Linux, LlamaServerArch.X64);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var b9198 = groups.Single(g => g.Build == "b9198");
        var variant = Assert.Single(b9198.Variants);
        Assert.Equal("llama-b9198-bin-ubuntu-vulkan-x64.tar.gz", variant.AssetName);
    }

    [Fact]
    public async Task DraftAndPrerelease_Excluded()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        Assert.DoesNotContain(groups, g => g.Build.Contains("draft"));
        Assert.DoesNotContain(groups, g => g.Build.Contains("rc"));
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responders.Enqueue(responder);

        public void EnqueueOk(string body, string? etag = null)
        {
            _responders.Enqueue(_ =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body),
                };
                if (etag is not null)
                    resp.Headers.TryAddWithoutValidation("ETag", etag);
                return resp;
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responders.Count == 0)
                throw new InvalidOperationException("No scripted response left.");
            try
            {
                return Task.FromResult(_responders.Dequeue()(request));
            }
            catch (HttpRequestException ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
