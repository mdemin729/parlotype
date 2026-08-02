using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// File-backed implementation of <see cref="ILlamaServerRegistry"/>.
/// Stores managed installs in <c>manifest.json</c> under the llama-servers
/// root; the active selector lives in <see cref="ISettingsService"/> under
/// <see cref="SettingsKeys.LlamaCppActiveInstall"/>.
/// </summary>
public sealed class JsonLlamaServerRegistry : ILlamaServerRegistry
{
    internal const int CurrentSchemaVersion = 1;
    internal const string ManifestFileName = "manifest.json";
    internal const string ManagedSelectorPrefix = "managed:";
    internal const string ManualSelector = "manual";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _rootDirectory;
    private readonly ISettingsService _settings;
    private readonly ILogger<JsonLlamaServerRegistry> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonLlamaServerRegistry(
        ISettingsService settings,
        ILogger<JsonLlamaServerRegistry> logger)
        : this(DefaultRootDirectory(), settings, logger) { }

    internal JsonLlamaServerRegistry(
        string rootDirectory,
        ISettingsService settings,
        ILogger<JsonLlamaServerRegistry> logger)
    {
        _rootDirectory = rootDirectory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary><c>%LOCALAPPDATA%\parlotype-data\llama-servers</c> on Windows; see <see cref="IAppPaths"/>.</summary>
    public static string DefaultRootDirectory() => AppPaths.Default.LlamaServerInstallsDirectory;

    public async Task<IReadOnlyList<LlamaServerInstall>> ListManagedAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            return manifest.Installs
                .Select(ToInstall)
                .OrderByDescending(i => i.InstalledAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LlamaServerInstall?> GetManagedAsync(
        string installId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var dto = manifest.Installs.FirstOrDefault(i => i.Id == installId);
            return dto is null ? null : ToInstall(dto);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddOrUpdateAsync(
        LlamaServerManagedInstallRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Id);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var dto = ToDto(record);
            var existingIndex = manifest.Installs.FindIndex(i => i.Id == record.Id);
            if (existingIndex >= 0)
                manifest.Installs[existingIndex] = dto;
            else
                manifest.Installs.Add(dto);

            await SaveManifestAsync(manifest, cancellationToken);
            _logger.LogInformation(
                "Manifest updated: {Id} ({Backend} {Os} {Arch})",
                record.Id, record.Backend, record.Os, record.Arch);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(
        string installId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken);
            var removed = manifest.Installs.RemoveAll(i => i.Id == installId);
            if (removed > 0)
            {
                await SaveManifestAsync(manifest, cancellationToken);
                _logger.LogInformation("Manifest entry removed: {Id}", installId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LlamaServerInstall?> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var selector = await _settings.GetAsync<string>(
            SettingsKeys.LlamaCppActiveInstall, cancellationToken);
        if (string.IsNullOrWhiteSpace(selector))
            return null;

        if (selector.StartsWith(ManagedSelectorPrefix, StringComparison.Ordinal))
        {
            var id = selector[ManagedSelectorPrefix.Length..];
            return await GetManagedAsync(id, cancellationToken);
        }

        if (selector == ManualSelector)
        {
            var folder = await _settings.GetAsync<string>(
                SettingsKeys.LlamaCppServerFolder, cancellationToken);
            if (string.IsNullOrWhiteSpace(folder))
                return null;

            return new LlamaServerInstall(
                Id: ManualSelector,
                Source: LlamaServerSource.Manual,
                Build: null,
                Backend: null,
                AbsolutePath: folder,
                InstalledAt: DateTimeOffset.MinValue,
                IsValid: Directory.Exists(folder));
        }

        _logger.LogWarning("Unrecognised active-install selector: {Selector}", selector);
        return null;
    }

    public async Task SetActiveAsync(
        string? installId,
        LlamaServerSource source,
        CancellationToken cancellationToken = default)
    {
        string? value = source switch
        {
            LlamaServerSource.Managed when !string.IsNullOrWhiteSpace(installId)
                => ManagedSelectorPrefix + installId,
            LlamaServerSource.Managed
                => null,
            LlamaServerSource.Manual when string.IsNullOrWhiteSpace(installId)
                => ManualSelector,
            LlamaServerSource.Manual
                => throw new ArgumentException(
                    "Manual mode does not take an install id.", nameof(installId)),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        await _settings.SetAsync(SettingsKeys.LlamaCppActiveInstall, value, cancellationToken);
        _logger.LogInformation("Active install set to: {Selector}", value ?? "(none)");
    }

    private LlamaServerInstall ToInstall(ManagedInstallDto dto)
    {
        var absolutePath = Path.Combine(_rootDirectory, dto.Id);
        return new LlamaServerInstall(
            Id: dto.Id,
            Source: LlamaServerSource.Managed,
            Build: dto.Build,
            Backend: ParseEnum<LlamaServerBackend>(dto.Backend),
            AbsolutePath: absolutePath,
            InstalledAt: dto.InstalledAt,
            IsValid: Directory.Exists(absolutePath));
    }

    private static ManagedInstallDto ToDto(LlamaServerManagedInstallRecord record) => new(
        Id: record.Id,
        Build: record.Build,
        Backend: record.Backend.ToString(),
        Os: record.Os.ToString(),
        Arch: record.Arch.ToString(),
        AssetName: record.AssetName,
        CompanionAssetName: record.CompanionAssetName,
        Sha256: record.Sha256,
        CompanionSha256: record.CompanionSha256,
        InstalledAt: record.InstalledAt);

    private static T ParseEnum<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : default;

    private async Task<ManifestDto> LoadManifestAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_rootDirectory, ManifestFileName);
        if (!File.Exists(path))
            return ManifestDto.Empty();

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read llama-server manifest at {Path}", path);
            return ManifestDto.Empty();
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<ManifestDto>(json, JsonOptions);
            if (manifest is null || manifest.Installs is null)
                return ManifestDto.Empty();
            return manifest;
        }
        catch (JsonException ex)
        {
            QuarantineCorruptManifest(path, ex);
            return ManifestDto.Empty();
        }
    }

    private async Task SaveManifestAsync(ManifestDto manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDirectory);
        var path = Path.Combine(_rootDirectory, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private void QuarantineCorruptManifest(string path, Exception cause)
    {
        var backupPath = $"{path}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        try
        {
            File.Move(path, backupPath, overwrite: false);
            _logger.LogWarning(cause,
                "llama-server manifest at {Path} was corrupt; moved to {Backup} and starting fresh.",
                path, backupPath);
        }
        catch (Exception moveEx)
        {
            _logger.LogError(moveEx,
                "llama-server manifest at {Path} was corrupt and could not be moved aside.",
                path);
        }
    }

    internal sealed record ManifestDto(int Version, List<ManagedInstallDto> Installs)
    {
        public static ManifestDto Empty() => new(CurrentSchemaVersion, new List<ManagedInstallDto>());
    }

    internal sealed record ManagedInstallDto(
        string Id,
        string Build,
        string Backend,
        string Os,
        string Arch,
        string AssetName,
        string? CompanionAssetName,
        string? Sha256,
        string? CompanionSha256,
        DateTimeOffset InstalledAt);
}
