using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Controllable mock for <see cref="IGlobalHotkeyService"/> that allows
/// tests to simulate hotkey press/release events.
/// </summary>
public sealed class MockGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public HotkeyBinding CurrentBinding { get; private set; } = HotkeyBinding.Default;
    public ActivationMode Mode { get; set; } = ActivationMode.PushToTalk;

    public bool IsStarted { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = false;
        return Task.CompletedTask;
    }

    public void UpdateBinding(HotkeyBinding binding)
    {
        CurrentBinding = binding;
    }

    /// <summary>Simulates a hotkey press event.</summary>
    public void SimulatePress() => HotkeyPressed?.Invoke(this, EventArgs.Empty);

    /// <summary>Simulates a hotkey release event.</summary>
    public void SimulateRelease() => HotkeyReleased?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}
