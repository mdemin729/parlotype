using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech.LlamaServer;

namespace Parlotype.Platform.Speech.LlamaServer;

/// <summary>
/// Fetches the llama.cpp release catalog from GitHub. Caches the parsed result
/// on disk with an ETag so polite usage stays well under the 60 req/hr
/// unauthenticated rate limit. Filters variants down to the current OS/arch.
/// Unknown backends are dropped from the result (and not surfaced) — the
/// parser still recognises them so logs/diagnostics see the full picture.
/// </summary>
public sealed class GitHubLlamaServerCatalog : ILlamaServerCatalog
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=10";
    internal const string CacheDirName = ".cache";
    internal const string CacheFileName = "releases.json";

    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    private static readonly string DefaultUserAgent =
        $"parlotype/{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev"}";

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions GitHubJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _httpClient;
    private readonly string _rootDirectory;
    private readonly ILogger<GitHubLlamaServerCatalog> _logger;
    private readonly Func<DateTimeOffset> _now;
    private readonly LlamaServerOs _currentOs;
    private readonly LlamaServerArch _currentArch;
    private readonly TimeSpan _cacheTtl;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GitHubLlamaServerCatalog(
        HttpClient httpClient,
        ILogger<GitHubLlamaServerCatalog> logger)
        : this(
            httpClient,
            JsonLlamaServerRegistry.DefaultRootDirectory(),
            logger,
            () => DateTimeOffset.UtcNow,
            DetectOs(),
            DetectArch(),
            DefaultCacheTtl)
    { }

    internal GitHubLlamaServerCatalog(
        HttpClient httpClient,
        string rootDirectory,
        ILogger<GitHubLlamaServerCatalog> logger,
        Func<DateTimeOffset> nowProvider,
        LlamaServerOs currentOs,
        LlamaServerArch currentArch,
        TimeSpan cacheTtl)
    {
        _httpClient = httpClient;
        _rootDirectory = rootDirectory;
        _logger = logger;
        _now = nowProvider;
        _currentOs = currentOs;
        _currentArch = currentArch;
        _cacheTtl = cacheTtl;
    }

    public async Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchAsync(
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var cache = await LoadCacheAsync(cancellationToken);
            var now = _now();

            if (!forceRefresh && cache is not null && (now - cache.FetchedAt) < _cacheTtl)
            {
                _logger.LogDebug("Returning cached llama.cpp catalog (age: {Age})",
                    now - cache.FetchedAt);
                return Project(cache);
            }

            return await FetchFromGitHubAsync(cache, now, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchFromGitHubAsync(
        CacheFileDto? cache,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (cache?.ETag is { Length: > 0 } etag)
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            if (cache is not null)
            {
                _logger.LogWarning(ex,
                    "Failed to refresh llama.cpp catalog; falling back to cached snapshot from {When}.",
                    cache.FetchedAt);
                return Project(cache);
            }
            _logger.LogError(ex, "Failed to fetch llama.cpp catalog and no cache is available.");
            throw;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified && cache is not null)
            {
                _logger.LogDebug("llama.cpp catalog unchanged (304); extending cache.");
                var refreshed = cache with { FetchedAt = now };
                await SaveCacheAsync(refreshed, cancellationToken);
                return Project(refreshed);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (cache is not null)
                {
                    _logger.LogWarning(
                        "GitHub releases API returned {Status}; falling back to cached snapshot.",
                        (int)response.StatusCode);
                    return Project(cache);
                }
                response.EnsureSuccessStatusCode();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = ParseGitHubBody(body);
            var newCache = new CacheFileDto(
                FetchedAt: now,
                ETag: FormatETag(response.Headers.ETag),
                Releases: releases);
            await SaveCacheAsync(newCache, cancellationToken);
            return Project(newCache);
        }
    }

    /// <summary>
    /// Projects the cached snapshot through the current OS+arch filter. Done
    /// at read time (not write time) so a single cache file works after the
    /// user changes machines or we add support for additional platforms.
    /// </summary>
    private IReadOnlyList<LlamaServerReleaseGroup> Project(CacheFileDto cache)
    {
        var output = new List<LlamaServerReleaseGroup>(cache.Releases.Count);
        foreach (var group in cache.Releases)
        {
            var filtered = group.Variants
                .Where(v => v.Os == _currentOs
                            && v.Arch == _currentArch
                            && v.Backend != LlamaServerBackend.Unknown)
                .ToList();
            if (filtered.Count > 0)
                output.Add(new LlamaServerReleaseGroup(group.Build, filtered));
        }
        return output;
    }

    private List<CachedReleaseGroupDto> ParseGitHubBody(string body)
    {
        var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(body, GitHubJsonOptions)
                       ?? new List<GitHubReleaseDto>();

        var groups = new List<CachedReleaseGroupDto>();
        foreach (var release in releases)
        {
            if (release.Draft || release.Prerelease)
                continue;
            if (string.IsNullOrWhiteSpace(release.TagName))
                continue;

            var variants = BuildVariantsForRelease(release);
            if (variants.Count > 0)
                groups.Add(new CachedReleaseGroupDto(release.TagName, variants));
        }
        return groups;
    }

    private List<LlamaServerVariant> BuildVariantsForRelease(GitHubReleaseDto release)
    {
        var assets = release.Assets ?? new List<GitHubAssetDto>();
        var parsed = new List<(GitHubAssetDto Asset, LlamaServerAssetDescriptor Descriptor)>();
        var unknownNames = new List<string>();

        foreach (var asset in assets)
        {
            if (LlamaServerAssetParser.TryParse(asset.Name, out var desc))
                parsed.Add((asset, desc));
            else
                unknownNames.Add(asset.Name);
        }

        if (unknownNames.Count > 0)
            _logger.LogDebug(
                "Release {Tag}: skipped {Count} unrecognised asset(s): {Names}",
                release.TagName, unknownNames.Count, string.Join(", ", unknownNames));

        var companionsByKey = new Dictionary<(string CudaVersion, LlamaServerArch Arch), GitHubAssetDto>();
        foreach (var (asset, desc) in parsed)
        {
            if (desc.IsCompanion && desc.CudaVersion is not null)
                companionsByKey[(desc.CudaVersion, desc.Arch)] = asset;
        }

        var variants = new List<LlamaServerVariant>();
        foreach (var (asset, desc) in parsed)
        {
            if (desc.IsCompanion) continue;
            if (desc.Build is null) continue;

            string? compName = null, compUrl = null, compSha = null;
            long? compBytes = null;
            if (desc.CudaVersion is not null
                && companionsByKey.TryGetValue((desc.CudaVersion, desc.Arch), out var comp))
            {
                compName = comp.Name;
                compUrl = comp.BrowserDownloadUrl;
                compBytes = comp.Size;
                compSha = StripShaPrefix(comp.Digest);
            }

            variants.Add(new LlamaServerVariant(
                Build: desc.Build,
                Backend: desc.Backend,
                Os: desc.Os,
                Arch: desc.Arch,
                AssetName: asset.Name,
                Bytes: asset.Size,
                DownloadUrl: asset.BrowserDownloadUrl,
                Sha256: StripShaPrefix(asset.Digest),
                CompanionAssetName: compName,
                CompanionDownloadUrl: compUrl,
                CompanionBytes: compBytes,
                CompanionSha256: compSha));
        }
        return variants;
    }

    private async Task<CacheFileDto?> LoadCacheAsync(CancellationToken cancellationToken)
    {
        var path = GetCachePath();
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var cache = JsonSerializer.Deserialize<CacheFileDto>(json, CacheJsonOptions);
            if (cache?.Releases is null)
                return null;
            return cache;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read llama.cpp catalog cache at {Path}; ignoring.", path);
            return null;
        }
    }

    private async Task SaveCacheAsync(CacheFileDto cache, CancellationToken cancellationToken)
    {
        var dir = Path.Combine(_rootDirectory, CacheDirName);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, CacheFileName);
        var json = JsonSerializer.Serialize(cache, CacheJsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private string GetCachePath() => Path.Combine(_rootDirectory, CacheDirName, CacheFileName);

    /// <summary>
    /// Renders an <see cref="EntityTagHeaderValue"/> back to its on-the-wire
    /// form so the weak-validator indicator (<c>W/</c>) survives the round-trip.
    /// <c>HeaderValueETag.Tag</c> alone drops that prefix, which would violate
    /// RFC 7232 when we echo it via <c>If-None-Match</c>.
    /// </summary>
    private static string? FormatETag(System.Net.Http.Headers.EntityTagHeaderValue? etag)
    {
        if (etag is null) return null;
        return etag.IsWeak ? "W/" + etag.Tag : etag.Tag;
    }

    private static string? StripShaPrefix(string? digest)
    {
        if (string.IsNullOrEmpty(digest)) return null;
        const string prefix = "sha256:";
        return digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digest[prefix.Length..]
            : digest;
    }

    private static LlamaServerOs DetectOs()
    {
        if (OperatingSystem.IsWindows()) return LlamaServerOs.Windows;
        if (OperatingSystem.IsMacOS()) return LlamaServerOs.MacOs;
        if (OperatingSystem.IsLinux()) return LlamaServerOs.Linux;
        return LlamaServerOs.Unknown;
    }

    private static LlamaServerArch DetectArch() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => LlamaServerArch.X64,
        Architecture.Arm64 => LlamaServerArch.Arm64,
        _ => LlamaServerArch.Unknown,
    };

    internal sealed record CacheFileDto(
        DateTimeOffset FetchedAt,
        string? ETag,
        List<CachedReleaseGroupDto> Releases);

    internal sealed record CachedReleaseGroupDto(
        string Build,
        List<LlamaServerVariant> Variants);

    internal sealed record GitHubReleaseDto
    {
        public string TagName { get; init; } = "";
        public string? Name { get; init; }
        public bool Draft { get; init; }
        public bool Prerelease { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }
        public List<GitHubAssetDto>? Assets { get; init; }
    }

    internal sealed record GitHubAssetDto
    {
        public string Name { get; init; } = "";
        public string BrowserDownloadUrl { get; init; } = "";
        public long Size { get; init; }
        public string? Digest { get; init; }
    }
}
