using Parlotype.Core.LlamaServer;
using Parlotype.Platform.LlamaServer;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Installer stub that records calls and forwards results to a provided
/// registry so the VM can re-read state after install/uninstall.
/// </summary>
public sealed class MockLlamaServerInstaller : ILlamaServerInstaller
{
    private readonly ILlamaServerRegistry _registry;
    private readonly Func<DateTimeOffset> _now;

    public List<LlamaServerVariant> InstalledVariants { get; } = new();
    public List<string> UninstalledIds { get; } = new();
    public Exception? InstallError { get; set; }
    public Exception? UninstallError { get; set; }

    public MockLlamaServerInstaller(ILlamaServerRegistry registry, Func<DateTimeOffset>? now = null)
    {
        _registry = registry;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<LlamaServerInstall> InstallAsync(
        LlamaServerVariant variant,
        IProgress<LlamaServerInstallProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (InstallError is not null) throw InstallError;
        InstalledVariants.Add(variant);

        // Use the real id derivation so VM matching against the catalog works.
        var id = LlamaServerInstaller.BuildInstallId(variant);
        var record = new LlamaServerManagedInstallRecord(
            Id: id,
            Build: variant.Build,
            Backend: variant.Backend,
            Os: variant.Os,
            Arch: variant.Arch,
            AssetName: variant.AssetName,
            CompanionAssetName: variant.CompanionAssetName,
            Sha256: variant.Sha256,
            CompanionSha256: variant.CompanionSha256,
            InstalledAt: _now());
        await _registry.AddOrUpdateAsync(record, cancellationToken);
        return (await _registry.GetManagedAsync(id, cancellationToken))!;
    }

    public async Task UninstallAsync(string installId, CancellationToken cancellationToken = default)
    {
        if (UninstallError is not null) throw UninstallError;
        UninstalledIds.Add(installId);
        await _registry.RemoveAsync(installId, cancellationToken);
    }
}
