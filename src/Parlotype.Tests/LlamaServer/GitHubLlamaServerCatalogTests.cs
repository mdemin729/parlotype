using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.LlamaServer;
using Parlotype.Platform.LlamaServer;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

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
        Assert.Equal(3, groups.Count); // b9198 + b9197 + the pre-release; the draft is excluded
        Assert.Equal("b9198", groups[0].Build);
        Assert.Equal("b9197", groups[1].Build);
        Assert.Equal("b9195-rc1", groups[2].Build);
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
        Assert.Equal(3, groups.Count);
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
    public async Task Drafts_AreExcluded_Prereleases_AreNot()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody);

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        Assert.DoesNotContain(groups, g => g.Build.Contains("draft"));
        Assert.Contains(groups, g => g.Build.Contains("rc"));
    }

    // --- The release scheme llama.cpp switched to in late 2026 ---
    //
    // Recorded 2026-11-13 from the live API and trimmed to three releases: the
    // non-pre-release "v1.0.0", which carries only nightly-tag.txt, and two b{N}
    // builds, both flagged prerelease. Under the old "skip pre-releases" rule
    // this response parsed to an empty catalog, which is what users saw.

    private static string ReadNovember2026Fixture() => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "llama-cpp-releases-2026-11.json"));

    [Fact]
    public async Task PrereleaseBuilds_AreListed_AndTheBinarylessStableReleaseIsNot()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(ReadNovember2026Fixture());

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        Assert.Equal(["b12360", "b12359"], groups.Select(g => g.Build));
    }

    [Fact]
    public async Task PrereleaseBuild_OffersTheWindowsX64Variants_WithCudaCompanions()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(ReadNovember2026Fixture());

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var variants = groups.Single(g => g.Build == "b12360").Variants;
        Assert.Equal(
            [
                "llama-b12360-bin-win-cpu-x64.zip",
                "llama-b12360-bin-win-cuda-12.4-x64.zip",
                "llama-b12360-bin-win-cuda-13.5-x64.zip",
                "llama-b12360-bin-win-rocm-10.0-x64.zip",
                "llama-b12360-bin-win-sycl-x64.zip",
                "llama-b12360-bin-win-vulkan-x64.zip",
            ],
            variants.Select(v => v.AssetName).Order());

        var cuda13 = variants.Single(v => v.Backend == LlamaServerBackend.Cuda13);
        Assert.Equal("cudart-llama-bin-win-cuda-13.5-x64.zip", cuda13.CompanionAssetName);
        Assert.Equal(64, cuda13.Sha256?.Length);

        // The AMD build was renamed from hip-radeon to rocm-{version}.
        Assert.Equal(LlamaServerBackend.Hip, variants.Single(v => v.AssetName.Contains("rocm")).Backend);
        Assert.Null(variants.Single(v => v.Backend == LlamaServerBackend.Cpu).CompanionAssetName);
    }

    [Fact]
    public async Task PrereleaseBuild_OnWindowsArm64_PairsTheArm64CudaCompanion()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(ReadNovember2026Fixture());

        var catalog = NewCatalog(handler, LlamaServerOs.Windows, LlamaServerArch.Arm64);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        var cuda = groups[0].Variants.Single(v => v.Backend == LlamaServerBackend.Cuda13);
        Assert.Equal("llama-b12360-bin-win-cuda-13.5-arm64.zip", cuda.AssetName);
        Assert.Equal("cudart-llama-bin-win-cuda-13.5-arm64.zip", cuda.CompanionAssetName);
    }

    // --- Cache versioning ---

    private void WriteCacheFile(string json)
    {
        var dir = Path.Combine(_root, GitHubLlamaServerCatalog.CacheDirName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, GitHubLlamaServerCatalog.CacheFileName), json);
    }

    /// <summary>
    /// The cache holds the parsed list beside the response's ETag. A list written
    /// by older parsing rules must not be revalidated: upstream would answer 304
    /// and the stale list (here the empty one the pre-release filter produced)
    /// would be served again, indefinitely.
    /// </summary>
    [Fact]
    public async Task CacheFromAnOlderParser_IsIgnored_EvenWithinItsTtl()
    {
        // What the previous build left on disk: no version field, no releases.
        WriteCacheFile($$"""
            {
              "fetchedAt": "{{_now.AddMinutes(-5):O}}",
              "eTag": "W/\"old\"",
              "releases": []
            }
            """);
        var handler = new ScriptedHandler();
        handler.Enqueue(req =>
        {
            Assert.False(req.Headers.Contains("If-None-Match"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ReadNovember2026Fixture()),
            };
        });

        var catalog = NewCatalog(handler);
        var groups = await catalog.FetchAsync(forceRefresh: false);

        Assert.Single(handler.Requests);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public async Task CacheIsWrittenWithTheCurrentParserVersion()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueOk(_fixtureBody, etag: "W/\"v1\"");

        await NewCatalog(handler).FetchAsync(forceRefresh: false);

        var path = Path.Combine(
            _root, GitHubLlamaServerCatalog.CacheDirName, GitHubLlamaServerCatalog.CacheFileName);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(
            GitHubLlamaServerCatalog.CacheVersion,
            doc.RootElement.GetProperty("version").GetInt32());
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
