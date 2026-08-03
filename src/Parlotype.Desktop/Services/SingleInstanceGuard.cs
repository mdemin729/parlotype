using Velopack.Logging;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Lets only one Parlotype run per desktop session, and turns every further
/// launch into a request to show the running one.
/// </summary>
/// <remarks>
/// <para>
/// Parlotype is tray-first and normally has no window on screen, so a second
/// launch looks like nothing happening — and the user launches again. Every
/// extra process registers the same global hotkey, opens the same microphone
/// and writes the same settings.json, so which one reacts to a keypress
/// (or whether several do) is undefined.
/// </para>
/// <para>
/// The lock is a named mutex rather than a scan of running processes: it is
/// atomic, so two launches racing at login cannot both decide they are first,
/// and it disappears with the process, so a crash or a kill leaves nothing to
/// clean up. Activation rides on a named event the primary owns; the secondary
/// sets it and exits.
/// </para>
/// <para>
/// Both names are session-scoped (<c>Local\</c>), which is what the conflict
/// actually is: hotkeys, audio devices and the tray are per-session, so a second
/// signed-in user gets their own instance. Everything here fails open — a guard
/// that cannot take its lock reports <see cref="IsPrimary"/> anyway, because a
/// broken guard must never be the reason the app won't start.
/// </para>
/// </remarks>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>Lock name for the shipping app. Tests pass their own.</summary>
    public const string DefaultName = "Parlotype.SingleInstance";

    private readonly string _name;
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _activation;
    private readonly ManualResetEvent _shutdown = new(initialState: false);
    private bool _listening;
    private bool _disposed;

    private SingleInstanceGuard(string name, bool isPrimary, Mutex? mutex, EventWaitHandle? activation)
    {
        _name = name;
        IsPrimary = isPrimary;
        _mutex = mutex;
        _activation = activation;
    }

    /// <summary>
    /// True when this process holds the lock and should start normally; false
    /// when another instance already runs and this one should
    /// <see cref="SignalPrimary"/> and exit.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Takes the single-instance lock, or reports that another process holds it.
    /// </summary>
    /// <param name="name">Lock name. Override only in tests, so a running
    /// Parlotype on the developer's machine cannot fail the test run.</param>
    public static SingleInstanceGuard Acquire(string name = DefaultName)
    {
        Mutex? mutex = null;

        try
        {
            mutex = new Mutex(initiallyOwned: false, Scoped(name));

            bool primary;
            try
            {
                primary = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner died without releasing it — a crash, a kill,
                // an installer replacing the running app. The wait still succeeded
                // and we now own the mutex, so this is a normal start.
                primary = true;
            }

            if (!primary)
            {
                mutex.Dispose();
                return new SingleInstanceGuard(name, isPrimary: false, mutex: null, activation: null);
            }

            // Created here, before Avalonia starts, rather than in
            // ListenForActivation: the event exists from the moment we win the
            // mutex, so a second launch during our (multi-second) startup still
            // finds it. Auto-reset events stay signalled until someone waits, so
            // that early signal is delivered once the listener thread starts.
            return new SingleInstanceGuard(name, isPrimary: true, mutex, CreateActivationEvent(name));
        }
        catch (Exception ex)
        {
            // A mutex we cannot create (locked-down policy, a name collision with
            // some other kernel object) must not block startup. Running is the
            // safe failure mode; running twice is a nuisance, not starting at all
            // is a broken app.
            VelopackFileLogger.Instance.LogWarning(ex, "Could not take the single-instance lock — starting anyway");
            mutex?.Dispose();
            return new SingleInstanceGuard(name, isPrimary: true, mutex: null, activation: null);
        }
    }

    /// <summary>
    /// Starts watching for other launches. <paramref name="onActivationRequested"/>
    /// is raised on a background thread every time another process calls
    /// <see cref="SignalPrimary"/>; it is expected to marshal to the UI thread.
    /// </summary>
    public void ListenForActivation(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);

        if (!IsPrimary || _activation is null || _listening)
            return;

        _listening = true;

        var thread = new Thread(() => Listen(_activation, onActivationRequested))
        {
            IsBackground = true,
            Name = "parlotype-single-instance",
        };
        thread.Start();
    }

    /// <summary>
    /// Asks the instance that holds the lock to come to the foreground. Returns
    /// false when there was nothing to signal — including on platforms without
    /// named events, where a second launch simply exits.
    /// </summary>
    public bool SignalPrimary()
    {
        if (IsPrimary || !OperatingSystem.IsWindows())
            return false;

        try
        {
            if (!EventWaitHandle.TryOpenExisting(Scoped(ActivationEventName(_name)), out var handle))
                return false;

            using (handle)
                return handle.Set();
        }
        catch (Exception ex)
        {
            // Worst case the user sees no window and launches again; that is
            // still better than a second instance fighting for the hotkey.
            VelopackFileLogger.Instance.LogWarning(ex, "Could not signal the running Parlotype instance");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Ends the listener thread, which owns and disposes the activation handle.
        _shutdown.Set();

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Disposal from a thread other than the one that took the mutex. The
            // handle close below (and process exit) releases it regardless.
        }

        _mutex.Dispose();
    }

    private void Listen(EventWaitHandle activation, Action onActivationRequested)
    {
        try
        {
            var handles = new WaitHandle[] { activation, _shutdown };

            while (WaitHandle.WaitAny(handles) == 0)
            {
                try
                {
                    onActivationRequested();
                }
                catch (Exception ex)
                {
                    // Keep listening: one failed activation should not make every
                    // later launch silent.
                    VelopackFileLogger.Instance.LogWarning(ex, "Handling a second-launch activation failed");
                }
            }
        }
        catch (Exception ex)
        {
            VelopackFileLogger.Instance.LogWarning(ex, "The single-instance listener stopped");
        }
        finally
        {
            // Only this thread waits on it, so disposing here cannot race a wait.
            activation.Dispose();
        }
    }

    private static EventWaitHandle? CreateActivationEvent(string name)
    {
        // Named events are a Windows primitive; .NET throws on Unix. macOS and
        // Linux therefore get the lock but no activation — a second launch exits
        // without bringing the first one forward.
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            return new EventWaitHandle(false, EventResetMode.AutoReset, Scoped(ActivationEventName(name)));
        }
        catch (Exception ex)
        {
            VelopackFileLogger.Instance.LogWarning(ex, "Could not create the single-instance activation event");
            return null;
        }
    }

    private static string ActivationEventName(string name) => name + ".Activate";

    /// <summary>
    /// Scopes the name to the logon session on Windows. Unix has no such
    /// namespace prefix, and .NET maps plain names to per-user shared memory
    /// there, which is the same intent.
    /// </summary>
    private static string Scoped(string name) => OperatingSystem.IsWindows() ? $@"Local\{name}" : name;
}
