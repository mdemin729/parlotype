using Parlotype.Core.LlamaServer;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// The model on disk, as far as the Speech cleanup page can tell. Scriptable:
/// the "user" can accept or decline the download, or it can fail.
/// </summary>
public sealed class MockSpeechCleanupModelInstaller : ISpeechCleanupModelInstaller
{
    public bool Installed { get; set; }

    /// <summary>What happens when the page asks to download: true installs, false declines.</summary>
    public bool UserAccepts { get; set; } = true;

    public Exception? InstallError { get; set; }
    public int InstallRequests { get; private set; }
    public int DeleteCount { get; private set; }

    public bool IsInstalled(SpeechCleanupModelInfo model) => Installed;

    public Task<bool> TryInstallAsync(SpeechCleanupModelInfo model, CancellationToken cancellationToken = default)
    {
        if (Installed)
            return Task.FromResult(true);

        InstallRequests++;
        if (InstallError is not null)
            return Task.FromException<bool>(InstallError);

        Installed = UserAccepts;
        return Task.FromResult(Installed);
    }

    public Task DeleteAsync(SpeechCleanupModelInfo model)
    {
        DeleteCount++;
        Installed = false;
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="ILlamaServerRegistry"/>: a list of managed installs and
/// which one is active.
/// </summary>
public sealed class MockLlamaServerRegistry : ILlamaServerRegistry
{
    private readonly List<LlamaServerInstall> _installs = [];

    public string? ActiveId { get; private set; }

    public void AddManaged(string id, bool isValid = true) =>
        _installs.Add(new LlamaServerInstall(
            id, LlamaServerSource.Managed, "b1", LlamaServerBackend.Cpu,
            $@"C:\fake\llama-servers\{id}", DateTimeOffset.UnixEpoch, isValid));

    public Task<IReadOnlyList<LlamaServerInstall>> ListManagedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LlamaServerInstall>>(_installs.ToList());

    public Task<LlamaServerInstall?> GetManagedAsync(string installId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_installs.FirstOrDefault(i => i.Id == installId));

    public Task AddOrUpdateAsync(LlamaServerManagedInstallRecord record, CancellationToken cancellationToken = default)
    {
        _installs.RemoveAll(i => i.Id == record.Id);
        _installs.Add(new LlamaServerInstall(
            record.Id, LlamaServerSource.Managed, record.Build, record.Backend,
            $@"C:\fake\llama-servers\{record.Id}", record.InstalledAt, IsValid: true));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string installId, CancellationToken cancellationToken = default)
    {
        _installs.RemoveAll(i => i.Id == installId);
        return Task.CompletedTask;
    }

    public Task<LlamaServerInstall?> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_installs.FirstOrDefault(i => i.Id == ActiveId));

    public Task SetActiveAsync(
        string? installId, LlamaServerSource source, CancellationToken cancellationToken = default)
    {
        ActiveId = installId;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cleanup service double whose readiness follows the two things the real one
/// checks: the model file and an active, valid server install.
/// </summary>
public sealed class MockSpeechCleanupService(
    MockSpeechCleanupModelInstaller model, MockLlamaServerRegistry registry) : ISpeechCleanupService
{
    public int UnloadCount { get; private set; }

    public async Task<SpeechCleanupReadiness> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        if (!model.Installed)
            return SpeechCleanupReadiness.ModelMissing;

        var active = await registry.GetActiveAsync(cancellationToken);
        return active is { IsValid: true }
            ? SpeechCleanupReadiness.Ready
            : SpeechCleanupReadiness.ServerMissing;
    }

    public Task PrepareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> CleanAsync(
        string transcript, SpeechCleanupOptions options, CancellationToken cancellationToken = default) =>
        Task.FromResult(transcript);

    public Task UnloadAsync()
    {
        UnloadCount++;
        return Task.CompletedTask;
    }
}
