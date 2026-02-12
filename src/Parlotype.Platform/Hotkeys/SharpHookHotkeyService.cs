using Parlotype.Core.Hotkeys;

namespace Parlotype.Platform.Hotkeys;

/// <summary>Global hotkey listener using SharpHook.</summary>
public sealed class SharpHookHotkeyService : IGlobalHotkeyService
{
#pragma warning disable CS0067 // Event is never used (stub implementation)
    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;
#pragma warning restore CS0067

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}
