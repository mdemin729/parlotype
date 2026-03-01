using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Parlotype.Core.TextInjection;

namespace Parlotype.Platform.TextInjection;

/// <summary>
/// Tracks the foreground window using SetWinEventHook so we can restore
/// focus to the last non-Parlotype window before injecting text.
/// </summary>
public sealed class Win32TargetWindowTracker : ITargetWindowTracker
{
    private readonly ILogger<Win32TargetWindowTracker> _logger;
    private readonly nint _ownProcessId;
    private nint _hook;
    private nint _targetWindow;

    // Must be stored as a field to prevent GC of the delegate while the hook is active.
    private readonly WinEventDelegate _winEventProc;

    public Win32TargetWindowTracker(ILogger<Win32TargetWindowTracker> logger)
    {
        _logger = logger;
        _ownProcessId = (nint)Environment.ProcessId;
        _winEventProc = OnForegroundChanged;

        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc,
            0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_hook == IntPtr.Zero)
            _logger.LogWarning("Failed to install foreground window hook");
        else
            _logger.LogDebug("Foreground window tracker installed");
    }

    public nint? TargetWindow
    {
        get
        {
            var hwnd = Volatile.Read(ref _targetWindow);
            return hwnd == 0 ? null : hwnd;
        }
    }

    public bool ActivateTargetWindow()
    {
        var hwnd = Volatile.Read(ref _targetWindow);
        if (hwnd == 0)
        {
            _logger.LogDebug("No target window tracked — skipping activation");
            return false;
        }

        if (!IsWindow(hwnd))
        {
            _logger.LogDebug("Target window {Handle} no longer valid", hwnd);
            Volatile.Write(ref _targetWindow, 0);
            return false;
        }

        var result = SetForegroundWindow(hwnd);
        _logger.LogDebug("SetForegroundWindow({Handle}) returned {Result}", hwnd, result);
        return result;
    }

    private void OnForegroundChanged(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero)
            return;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)_ownProcessId)
            return;

        var previous = Volatile.Read(ref _targetWindow);
        if (previous == hwnd)
            return;

        Volatile.Write(ref _targetWindow, hwnd);
        var processName = TryGetProcessName(pid);
        _logger.LogInformation("Target window set to {Handle} (PID {Pid}, Name {Name})", hwnd, pid, processName);
    }

    private static string TryGetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
            _logger.LogDebug("Foreground window tracker uninstalled");
        }
    }

    // --- Win32 interop ---

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate void WinEventDelegate(
        nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
