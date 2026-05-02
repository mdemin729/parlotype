using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public HotkeyBinding CurrentBinding { get; private set; } = HotkeyBinding.Default;
    public ActivationMode Mode { get; set; } = ActivationMode.PushToTalk;
    public bool IsStarted { get; private set; }

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

    public void UpdateBinding(HotkeyBinding binding) => CurrentBinding = binding;

    public void SimulatePress() => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    public void SimulateRelease() => HotkeyReleased?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}
