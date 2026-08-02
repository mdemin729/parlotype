namespace Parlotype.Core.Updates;

/// <summary>
/// Checks for, downloads, and applies application updates.
/// </summary>
/// <remarks>
/// <para>
/// The update check is the only outbound network request Parlotype makes in local
/// mode, so it is a product decision as much as an implementation detail (ADR-053).
/// Implementations must send nothing beyond what fetching the public release feed
/// requires: no machine identifier, no install id, no usage data, no custom headers
/// carrying user information. The check is a plain unauthenticated read of a public
/// endpoint, and it must be possible for the user to turn it off entirely via
/// <see cref="Settings.SettingsKeys.UpdatesCheckAutomatically"/>.
/// </para>
/// <para>
/// Failures never interrupt the user on the automatic path — no dialogs, no toasts.
/// They surface only through <see cref="Status"/>, which the Settings page renders,
/// and are reported plainly when the user asked for the check themselves.
/// </para>
/// </remarks>
public interface IUpdateService
{
    /// <summary>Latest known state. Never null; starts at <see cref="UpdateState.Idle"/>.</summary>
    UpdateStatus Status { get; }

    /// <summary>Raised on every <see cref="Status"/> transition. May fire on any thread.</summary>
    event EventHandler<UpdateStatus>? StatusChanged;

    /// <summary>
    /// The running version, or null when this build was not installed by Velopack.
    /// </summary>
    string? CurrentVersion { get; }

    /// <summary>
    /// Starts the background checker: one check shortly after startup, then on a
    /// recurring interval. Honours the user's opt-out and returns immediately —
    /// the work happens off the calling thread and never blocks startup.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the feed and, when a newer release exists, downloads it in the
    /// background so it can be applied on exit or on demand.
    /// </summary>
    /// <remarks>
    /// Downloading only <em>stages</em> the release. Nothing is installed until
    /// <see cref="ApplyOnExit"/> or <see cref="ApplyAndRestartAsync"/> runs — a
    /// staged package survives an ordinary quit-and-relaunch without being applied.
    /// </remarks>
    /// <param name="userInitiated">
    /// True when the user pressed "Check now". Automatic checks skip the network
    /// entirely if the user opted out, and swallow offline/HTTP errors; a
    /// user-initiated check always runs and reports what went wrong.
    /// </param>
    Task<UpdateStatus> CheckAsync(bool userInitiated, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands a staged update to the platform updater to install once this process
    /// exits, without relaunching. Returns false when nothing is staged.
    /// </summary>
    /// <remarks>
    /// Call this from the shutdown path. It is deliberately synchronous: shutdown
    /// handlers are not a reliable place to await a continuation, and this only
    /// needs to hand off to the updater, not wait for it.
    /// </remarks>
    bool ApplyOnExit();

    /// <summary>
    /// Applies a downloaded update and restarts the app. Returns false when
    /// nothing is staged; otherwise the process exits and does not return.
    /// </summary>
    Task<bool> ApplyAndRestartAsync(CancellationToken cancellationToken = default);
}
