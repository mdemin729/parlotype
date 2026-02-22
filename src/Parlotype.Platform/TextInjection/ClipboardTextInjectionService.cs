using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Parlotype.Core.TextInjection;
using SharpHook;
using SharpHook.Data;

namespace Parlotype.Platform.TextInjection;

/// <summary>
/// Injects text via clipboard-with-restore: saves the current clipboard,
/// sets the transcribed text, simulates Ctrl+V, then restores the original content.
/// </summary>
public sealed class ClipboardTextInjectionService : ITextInjectionService
{
    private readonly ITargetWindowTracker _windowTracker;
    private readonly ILogger<ClipboardTextInjectionService> _logger;
    private readonly EventSimulator _simulator = new();

    public ClipboardTextInjectionService(
        ITargetWindowTracker windowTracker,
        ILogger<ClipboardTextInjectionService> logger)
    {
        _windowTracker = windowTracker;
        _logger = logger;
    }

    public async Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return;

        string? previousClipboard = null;

        try
        {
            previousClipboard = GetClipboardText();
            SetClipboardText(text);

            _windowTracker.ActivateTargetWindow();
            await Task.Delay(50, cancellationToken);

            SimulateCtrlV();

            // Give the target app time to process the paste
            await Task.Delay(150, cancellationToken);

            _logger.LogDebug("Injected {Length} characters via clipboard paste", text.Length);
        }
        finally
        {
            RestoreClipboard(previousClipboard);
        }
    }

    private void SimulateCtrlV()
    {
        var press = _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
        if (press != UioHookResult.Success)
            _logger.LogWarning("SimulateKeyPress(Ctrl) failed: {Result}", press);

        var vPress = _simulator.SimulateKeyPress(KeyCode.VcV);
        if (vPress != UioHookResult.Success)
            _logger.LogWarning("SimulateKeyPress(V) failed: {Result}", vPress);

        var vRelease = _simulator.SimulateKeyRelease(KeyCode.VcV);
        if (vRelease != UioHookResult.Success)
            _logger.LogWarning("SimulateKeyRelease(V) failed: {Result}", vRelease);

        var release = _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
        if (release != UioHookResult.Success)
            _logger.LogWarning("SimulateKeyRelease(Ctrl) failed: {Result}", release);
    }

    private void RestoreClipboard(string? previousContent)
    {
        try
        {
            if (previousContent is not null)
                SetClipboardText(previousContent);
            else
                EmptyClipboardContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore clipboard contents");
        }
    }

    // --- Win32 clipboard interop ---

    private static string? GetClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;

        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                return null;

            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
                return null;

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void SetClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new InvalidOperationException("Cannot open clipboard");

        try
        {
            EmptyClipboardNative();

            var bytes = (text.Length + 1) * sizeof(char);
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("GlobalAlloc failed for clipboard text");

            var ptr = GlobalLock(hGlobal);
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                // Null terminator
                Marshal.WriteInt16(ptr + text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("SetClipboardData failed");
            }
            // After successful SetClipboardData, the system owns the memory — do not free it.
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void EmptyClipboardContent()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return;

        try
        {
            EmptyClipboardNative();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", EntryPoint = "EmptyClipboard")]
    private static extern bool EmptyClipboardNative();

    [DllImport("kernel32.dll")]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint hMem);
}
