namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// Persists the list of managed llama.cpp server installs and which one is
/// active. Implementations back this with <c>manifest.json</c> and are the
/// source of truth (folder names are convenient but not load-bearing).
/// </summary>
public interface ILlamaServerRegistry
{
    /// <summary>Returns all managed installs, newest-first by install time.</summary>
    Task<IReadOnlyList<LlamaServerInstall>> ListManagedAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a specific managed install, or null if missing.</summary>
    Task<LlamaServerInstall?> GetManagedAsync(string installId, CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces a managed install entry in the manifest.</summary>
    Task AddOrUpdateAsync(LlamaServerManagedInstallRecord record, CancellationToken cancellationToken = default);

    /// <summary>Removes a managed install entry. No-op if absent.</summary>
    Task RemoveAsync(string installId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active install. When <see cref="LlamaServerSource.Manual"/>
    /// is active, <see cref="LlamaServerInstall.AbsolutePath"/> is the
    /// user-supplied folder. Null if no active selection exists.
    /// </summary>
    Task<LlamaServerInstall?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active install. Pass <c>installId = null</c> with
    /// <see cref="LlamaServerSource.Manual"/> to mark the manual folder
    /// active; pass a managed id with <see cref="LlamaServerSource.Managed"/>
    /// to switch to a managed install.
    /// </summary>
    Task SetActiveAsync(
        string? installId,
        LlamaServerSource source,
        CancellationToken cancellationToken = default);
}
