namespace Parlotype.Core.Updates;

/// <summary>Where the updater currently is.</summary>
public enum UpdateState
{
    /// <summary>No check has run yet in this session.</summary>
    Idle,

    /// <summary>
    /// This build cannot update itself — it was run from the IDE, from
    /// <c>dotnet run</c>, or from the portable zip rather than installed via
    /// <c>Setup.exe</c>. Every update operation is a no-op in this state.
    /// </summary>
    NotInstalled,

    Checking,
    UpToDate,

    /// <summary>A newer release exists but has not been downloaded yet.</summary>
    UpdateAvailable,

    Downloading,

    /// <summary>Downloaded and staged. Applies on next restart, or on demand.</summary>
    ReadyToApply,

    /// <summary>The last check or download failed. See <see cref="UpdateStatus.Message"/>.</summary>
    Failed,
}

/// <summary>An immutable snapshot of the updater's state, safe to hand to the UI.</summary>
/// <param name="State">Current phase.</param>
/// <param name="AvailableVersion">Version of the pending release, when one is known.</param>
/// <param name="LastCheckedUtc">
/// When the feed was last successfully reached — persisted across restarts, so this
/// may predate the current session.
/// </param>
/// <param name="Message">
/// Human-readable detail for <see cref="UpdateState.Failed"/>. Null otherwise.
/// </param>
public sealed record UpdateStatus(
    UpdateState State,
    string? AvailableVersion = null,
    DateTimeOffset? LastCheckedUtc = null,
    string? Message = null);
