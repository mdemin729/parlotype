using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Parlotype.Core.LlamaServer;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// Fetches the llama.cpp release catalog from GitHub. Caches the parsed result
/// on disk with an ETag so polite usage stays well under the 60 req/hr
/// unauthenticated rate limit. Filters variants down to the current OS/arch.
/// Unknown backends are dropped from the result (and not surfaced) — the
/// parser still recognises them so logs/diagnostics see the full picture.
/// </summary>
/// <remarks>
/// A refresh is two requests: the ten newest builds, then upstream's newest
/// versioned release, which names the build to prefer (ADR-074). A third
/// request fetches that build's assets once it has left the first page.
/// </remarks>
public sealed partial class GitHubLlamaServerCatalog : ILlamaServerCatalog
{
    private const string RepositoryApiUrl = "https://api.github.com/repos/ggml-org/llama.cpp";
    private const string ReleasesUrl = RepositoryApiUrl + "/releases?per_page=10";

    /// <summary>
    /// GitHub answers this with the newest release that is neither a draft nor
    /// a pre-release, which for llama.cpp is the newest <c>v{X.Y.Z}</c>. That
    /// release leaves the first page of <see cref="ReleasesUrl"/> within a day.
    /// </summary>
    private const string LatestReleaseUrl = RepositoryApiUrl + "/releases/latest";

    /// <summary>The asset of a versioned release that holds the build tag it names.</summary>
    internal const string PointerAssetName = "nightly-tag.txt";

    internal const string CacheDirName = ".cache";
    internal const string CacheFileName = "releases.json";

    /// <summary>
    /// Version of the rules that turn a GitHub response into the cached list.
    /// Bump it whenever those rules change which releases or assets are kept.
    /// </summary>
    /// <remarks>
    /// The cache stores the <em>parsed</em> list next to the response's ETag, so
    /// a list produced by older rules would otherwise be confirmed forever: the
    /// conditional GET answers 304 for as long as upstream is unchanged, and the
    /// stale list is served again. That is how an empty catalog outlived the
    /// build that produced it. A cache written under another version is ignored
    /// and fetched afresh. Version 3 added the versioned release (ADR-074).
    /// </remarks>
    internal const int CacheVersion = 3;

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
        var (list, reachedGitHub) = await RefreshListAsync(cache, now, cancellationToken);

        // The list request failed and the cache stands in for it: GitHub is
        // unreachable or refusing us, so asking it two more questions is
        // pointless and, when rate-limited, unwelcome.
        if (!reachedGitHub)
            return Project(list);

        var refreshed = list with
        {
            VersionedRelease = await ResolveVersionedReleaseAsync(list, cancellationToken),
        };
        await SaveCacheAsync(refreshed, cancellationToken);
        return Project(refreshed);
    }

    /// <summary>
    /// Fetches the ten newest builds. Returns the snapshot to serve and whether
    /// GitHub answered; throws only when the request fails and there is no cache.
    /// </summary>
    private async Task<(CacheFileDto List, bool ReachedGitHub)> RefreshListAsync(
        CacheFileDto? cache,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var request = NewRequest(ReleasesUrl, cache?.ETag);

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
                return (cache, false);
            }
            _logger.LogError(ex, "Failed to fetch llama.cpp catalog and no cache is available.");
            throw;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified && cache is not null)
            {
                _logger.LogDebug("llama.cpp catalog unchanged (304); extending cache.");
                return (cache with { FetchedAt = now }, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                if (cache is not null)
                {
                    _logger.LogWarning(
                        "GitHub releases API returned {Status}; falling back to cached snapshot.",
                        (int)response.StatusCode);
                    return (cache, false);
                }
                response.EnsureSuccessStatusCode();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var releases = ParseGitHubBody(body);
            if (releases.Count == 0)
                _logger.LogWarning(
                    "The llama.cpp release list contained no installable build. " +
                    "Upstream may have changed how it publishes releases.");

            return (new CacheFileDto(
                Version: CacheVersion,
                FetchedAt: now,
                ETag: FormatETag(response.Headers.ETag),
                Releases: releases,
                VersionedRelease: cache?.VersionedRelease), true);
        }
    }

    /// <summary>
    /// Finds the build upstream's newest versioned release names, and that
    /// build's assets when the list no longer carries them (ADR-074).
    /// </summary>
    /// <remarks>
    /// Never throws for a failed request: the released build is a preference,
    /// and without it the catalog is the newest builds, as it was before. What
    /// was known last time is kept when GitHub cannot be asked.
    /// </remarks>
    private async Task<CachedVersionedReleaseDto?> ResolveVersionedReleaseAsync(
        CacheFileDto list,
        CancellationToken cancellationToken)
    {
        var known = list.VersionedRelease;
        var versioned = await FetchVersionedReleaseAsync(known, cancellationToken);
        if (versioned?.Build is not { } build)
            return versioned;

        // On the first page the list has the assets, and it is the fresher
        // copy: a build is listed a few minutes before its uploads finish.
        if (list.Releases.Any(g => g.Build == build) || versioned.Variants is not null)
            return versioned;

        var variants = await FetchVariantsByTagAsync(build, cancellationToken);
        return variants is { Count: > 0 } ? versioned with { Variants = variants } : versioned;
    }

    private async Task<CachedVersionedReleaseDto?> FetchVersionedReleaseAsync(
        CachedVersionedReleaseDto? known,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = NewRequest(LatestReleaseUrl, known?.ETag);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified && known is not null)
                return known;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GitHub returned {Status} for the latest llama.cpp release; keeping what was known.",
                    (int)response.StatusCode);
                return known;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubReleaseDto>(body, GitHubJsonOptions);
            if (release is null || release.Draft || release.Prerelease
                || string.IsNullOrWhiteSpace(release.TagName))
                return known;

            var build = ReadPointedBuild(release);
            if (build is null)
                _logger.LogWarning(
                    "llama.cpp release {Version} does not name a build that can be verified against " +
                    "{Pointer}; offering the newest builds only.",
                    release.TagName, PointerAssetName);

            // Kept even when no build could be read, so that the unchanged
            // release is answered with a 304 and not parsed and warned about
            // again on every refresh.
            return new CachedVersionedReleaseDto(
                ETag: FormatETag(response.Headers.ETag),
                Version: release.TagName,
                Build: build,
                Variants: build is not null && build == known?.Build ? known.Variants : null);
        }
        catch (Exception ex) when (IsRequestFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Failed to read the latest llama.cpp release; keeping what was known.");
            return known;
        }
    }

    private async Task<List<LlamaServerVariant>?> FetchVariantsByTagAsync(
        string build,
        CancellationToken cancellationToken)
    {
        try
        {
            // `build` matched BuildTagRegex, so it is safe to put in the path.
            using var request = NewRequest($"{RepositoryApiUrl}/releases/tags/{build}", etag: null);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GitHub returned {Status} for llama.cpp build {Build}; it is not offered as the release this time.",
                    (int)response.StatusCode, build);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubReleaseDto>(body, GitHubJsonOptions);
            return release is null || release.Draft ? null : BuildVariantsForRelease(release);
        }
        catch (Exception ex) when (IsRequestFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Failed to fetch llama.cpp build {Build}.", build);
            return null;
        }
    }

    /// <summary>
    /// Reads the build a versioned release names, or null when it cannot be
    /// established.
    /// </summary>
    /// <remarks>
    /// The build tag is the content of the release's <c>nightly-tag.txt</c>
    /// (<c>b12359</c> and a newline). The API does not return an asset's
    /// content, only its SHA-256, and the release notes name the same build in
    /// prose. So every build tag in the notes is a candidate and the digest
    /// decides: the one whose hash matches is, provably, what the pointer file
    /// says. If upstream rewords the notes or drops the file there is no match
    /// and no released build, never a wrong one.
    /// </remarks>
    internal static string? ReadPointedBuild(GitHubReleaseDto release)
    {
        var pointer = release.Assets?.FirstOrDefault(
            a => string.Equals(a.Name, PointerAssetName, StringComparison.Ordinal));
        var digest = StripShaPrefix(pointer?.Digest);
        if (digest is null || string.IsNullOrEmpty(release.Body))
            return null;

        foreach (var candidate in BuildTagRegex().Matches(release.Body).Select(m => m.Value).Distinct())
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(candidate + "\n")));
            if (string.Equals(hash, digest, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// A build tag standing on its own. The word boundaries keep commit hashes
    /// in a changelog (<c>b1b770d90</c>) from counting as one.
    /// </summary>
    [GeneratedRegex(@"\bb[0-9]{1,9}\b", RegexOptions.CultureInvariant)]
    private static partial Regex BuildTagRegex();

    private static HttpRequestMessage NewRequest(string url, string? etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (etag is { Length: > 0 })
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return request;
    }

    /// <summary>
    /// A request that did not work out, as opposed to the caller cancelling:
    /// no connection, a timeout (which HttpClient reports as a cancellation),
    /// or a body that is not the JSON we expect.
    /// </summary>
    private static bool IsRequestFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or JsonException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>
    /// Projects the cached snapshot through the current OS+arch filter. Done
    /// at read time (not write time) so a single cache file works after the
    /// user changes machines or we add support for additional platforms.
    /// </summary>
    private IReadOnlyList<LlamaServerReleaseGroup> Project(CacheFileDto cache)
    {
        var output = new List<LlamaServerReleaseGroup>(cache.Releases.Count + 1);

        // The released build goes first. Its assets are the list's while the
        // build is still on the first page, and the copy fetched by tag after.
        string? releasedBuild = null;
        if (cache.VersionedRelease is { Build: { } build } versioned)
        {
            var variants = cache.Releases.FirstOrDefault(g => g.Build == build)?.Variants
                           ?? versioned.Variants;
            var filtered = variants is null ? [] : FilterToThisMachine(variants);
            if (filtered.Count > 0)
            {
                output.Add(new LlamaServerReleaseGroup(build, filtered, versioned.Version));
                releasedBuild = build;
            }
        }

        foreach (var group in cache.Releases)
        {
            if (group.Build == releasedBuild)
                continue;

            var filtered = FilterToThisMachine(group.Variants);
            if (filtered.Count > 0)
                output.Add(new LlamaServerReleaseGroup(group.Build, filtered));
        }
        return output;
    }

    private List<LlamaServerVariant> FilterToThisMachine(IEnumerable<LlamaServerVariant> variants) =>
        variants
            .Where(v => v.Os == _currentOs
                        && v.Arch == _currentArch
                        && v.Backend != LlamaServerBackend.Unknown)
            .ToList();

    private List<CachedReleaseGroupDto> ParseGitHubBody(string body)
    {
        var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(body, GitHubJsonOptions)
                       ?? new List<GitHubReleaseDto>();

        var groups = new List<CachedReleaseGroupDto>();
        foreach (var release in releases)
        {
            // Drafts are unpublished. Pre-releases are NOT skipped: since late
            // 2026 llama.cpp marks every b{N} build as a pre-release ("nightly")
            // and keeps the non-pre-release slot for v{X.Y.Z} releases, which
            // carry no binaries, only a nightly-tag.txt naming a build. Skipping
            // pre-releases therefore skips every installable asset upstream
            // publishes. A release with nothing we can install drops out below.
            if (release.Draft)
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
            if (cache.Version != CacheVersion)
            {
                _logger.LogInformation(
                    "Ignoring llama.cpp catalog cache written by parser version {Found} (current: {Current}).",
                    cache.Version, CacheVersion);
                return null;
            }
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
        int Version,
        DateTimeOffset FetchedAt,
        string? ETag,
        List<CachedReleaseGroupDto> Releases,
        CachedVersionedReleaseDto? VersionedRelease = null);

    /// <summary>
    /// Upstream's newest versioned release as last seen at <c>/releases/latest</c>.
    /// </summary>
    /// <param name="ETag">Validator of that answer, for the conditional request.</param>
    /// <param name="Version">Its tag, e.g. <c>v1.0.0</c>.</param>
    /// <param name="Build">The build it names; null when none could be verified.</param>
    /// <param name="Variants">
    /// That build's assets, unfiltered, when they had to be fetched by tag
    /// because the build was no longer in <see cref="CacheFileDto.Releases"/>.
    /// </param>
    internal sealed record CachedVersionedReleaseDto(
        string? ETag,
        string Version,
        string? Build,
        List<LlamaServerVariant>? Variants);

    internal sealed record CachedReleaseGroupDto(
        string Build,
        List<LlamaServerVariant> Variants);

    internal sealed record GitHubReleaseDto
    {
        public string TagName { get; init; } = "";
        public string? Name { get; init; }
        public string? Body { get; init; }
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
