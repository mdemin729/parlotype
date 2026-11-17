using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Parlotype.Core.TextInjection;
using Parlotype.Platform.MacOs;

namespace Parlotype.Platform.TextInjection;

/// <summary>
/// Tracks the frontmost macOS application so focus can be handed back to it
/// before text is pasted (ADR-077).
/// </summary>
/// <remarks>
/// <para>
/// macOS activates applications, not windows, so the "target window" here is a
/// process id: reactivating the application restores its key window, which is
/// where the user's caret was.
/// </para>
/// <para>
/// The frontmost application is sampled on a timer rather than observed.
/// Observing it means registering an Objective-C object with
/// <c>NSWorkspace</c>'s notification centre, which needs a class built at run
/// time; a few message sends every 250 ms cost nothing measurable and cannot
/// leave a dangling observer behind. The sample only has to be fresher than the
/// time it takes a person to click Parlotype's widget and finish speaking.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacOsTargetWindowTracker : ITargetWindowTracker
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    /// <summary><c>NSApplicationActivateIgnoringOtherApps</c>.</summary>
    private const nint ActivateIgnoringOtherApps = 1 << 1;

    private readonly ILogger<MacOsTargetWindowTracker> _logger;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly Timer _timer;

    private int _targetProcessId;
    private bool _disposed;

    public MacOsTargetWindowTracker(ILogger<MacOsTargetWindowTracker> logger)
    {
        _logger = logger;
        MacOsNative.EnsureFrameworkLoaded("AppKit");

        Sample();
        _timer = new Timer(_ => Sample(), null, SampleInterval, SampleInterval);
        _logger.LogDebug("Frontmost application tracker installed");
    }

    public nint? TargetWindow
    {
        get
        {
            var pid = Volatile.Read(ref _targetProcessId);
            return pid == 0 ? null : pid;
        }
    }

    public bool ActivateTargetWindow()
    {
        var pid = Volatile.Read(ref _targetProcessId);
        if (pid == 0)
        {
            _logger.LogDebug("No target application tracked — skipping activation");
            return false;
        }

        try
        {
            using var pool = MacOsNative.AutoreleasePool.Create();

            // The usual case: dictation was started with the hotkey, the widget
            // was shown without taking focus, and the target never stopped being
            // frontmost. Activating it again would only make its windows flicker.
            if (FrontmostProcessId() == pid)
                return true;

            var application = MacOsNative.SendInt32(
                MacOsNative.Class("NSRunningApplication"),
                MacOsNative.Selector("runningApplicationWithProcessIdentifier:"),
                pid);

            if (application == 0)
            {
                _logger.LogDebug("Target application {Pid} is no longer running", pid);
                Volatile.Write(ref _targetProcessId, 0);
                return false;
            }

            var activated = MacOsNative.AsBool(MacOsNative.Send(
                application, MacOsNative.Selector("activateWithOptions:"), ActivateIgnoringOtherApps));

            _logger.LogDebug("Activating application {Pid} returned {Result}", pid, activated);
            return activated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not activate the target application");
            return false;
        }
    }

    private void Sample()
    {
        if (_disposed)
            return;

        try
        {
            using var pool = MacOsNative.AutoreleasePool.Create();

            var pid = FrontmostProcessId();
            if (pid <= 0 || pid == _ownProcessId || pid == Volatile.Read(ref _targetProcessId))
                return;

            Volatile.Write(ref _targetProcessId, pid);
            _logger.LogInformation("Target application set to PID {Pid}", pid);
        }
        catch (Exception ex)
        {
            // Runs on a timer thread: an escaping exception would end the process.
            _logger.LogDebug(ex, "Could not read the frontmost application");
        }
    }

    /// <summary>Must be called inside an autorelease pool.</summary>
    private static int FrontmostProcessId()
    {
        var workspace = MacOsNative.Send(MacOsNative.Class("NSWorkspace"), MacOsNative.Selector("sharedWorkspace"));
        if (workspace == 0)
            return 0;

        var application = MacOsNative.Send(workspace, MacOsNative.Selector("frontmostApplication"));
        return application == 0
            ? 0
            : (int)MacOsNative.Send(application, MacOsNative.Selector("processIdentifier"));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
        _logger.LogDebug("Frontmost application tracker uninstalled");
    }
}
