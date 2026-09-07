namespace Parlotype.Core.Settings;

/// <summary>
/// The single source of truth for every on-disk location Parlotype writes to.
/// </summary>
/// <remarks>
/// <para>
/// Parlotype ships as a Velopack package (ADR-053), and Velopack owns
/// <c>%LOCALAPPDATA%\Parlotype</c> outright: it replaces <c>current\</c> on every
/// update and deletes the <em>entire</em> pack folder on uninstall — and on a
/// re-run of <c>Setup.exe</c>. Nothing user-owned may live under it. Every path
/// on this interface therefore resolves outside the pack folder, and all writes
/// in the app must go through here rather than composing paths ad hoc.
/// </para>
/// <para>
/// Implementations must not create directories as a side effect of resolving a
/// path; callers do that at the point of first write.
/// </para>
/// </remarks>
public interface IAppPaths
{
    /// <summary>
    /// Root for app-managed data that is expensive to re-acquire — models and
    /// downloaded sidecar binaries.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Root for user-configured state: settings, secrets, saved prompts, window
    /// chrome. Equal to <see cref="DataDirectory"/> on Windows; a distinct
    /// config root on Linux, where XDG separates the two.
    /// </summary>
    string SettingsDirectory { get; }

    /// <summary>Speech-model cache — Whisper GGML, Parakeet, and Gemma 4 files.</summary>
    string ModelsDirectory { get; }

    /// <summary>Rolling log files.</summary>
    string LogsDirectory { get; }

    /// <summary>
    /// Root holding all managed <c>llama-server</c> installs and their manifest.
    /// Each install is a subfolder named by its install id (e.g.
    /// <c>b9198-win-vulkan-x64</c>) holding <c>llama-server.exe</c>. This is the
    /// <em>only</em> llama-server location Parlotype writes; a manually downloaded
    /// build lives wherever the user put it, recorded in
    /// <see cref="SettingsKeys.LlamaCppServerFolder"/> (ADR-066).
    /// </summary>
    string LlamaServerInstallsDirectory { get; }

    /// <summary><c>settings.json</c> — long-lived user settings.</summary>
    string SettingsFilePath { get; }

    /// <summary><c>secrets.json</c> — cloud-provider API keys, encrypted at rest where the OS allows.</summary>
    string SecretsFilePath { get; }

    /// <summary><c>window-state.json</c> — window position, written on every drag (ADR-040).</summary>
    string WindowStateFilePath { get; }
}
