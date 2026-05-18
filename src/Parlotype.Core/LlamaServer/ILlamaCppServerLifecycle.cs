namespace Parlotype.Core.LlamaServer;

/// <summary>
/// Lets the installer stop a running sidecar before deleting or replacing its
/// files. On Windows, the EXE is locked while the process holds it; uninstall
/// and switch-active must release that lock before touching the folder.
/// </summary>
public interface ILlamaCppServerLifecycle
{
    /// <summary>
    /// Stops the sidecar if one is running. Safe to call when nothing is
    /// running. Returns when the process has exited (or after a short timeout).
    /// </summary>
    Task StopForReplacementAsync(CancellationToken cancellationToken = default);
}
