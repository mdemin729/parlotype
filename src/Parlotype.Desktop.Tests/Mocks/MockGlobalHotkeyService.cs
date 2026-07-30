using Parlotype.Core.Hotkeys;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? DictationStartRequested;
    public event EventHandler? DictationStopRequested;
    public event EventHandler? DictationCancelRequested;
    public event EventHandler? BindingsChanged;

    public IReadOnlyList<DictationHotkey> Bindings { get; private set; } = DictationHotkeyDefaults.All;
    public bool IsStarted { get; private set; }

    /// <summary>Last value pushed in via <see cref="SetDictationActive"/>.</summary>
    public bool DictationActive { get; private set; }

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

    public void UpdateBindings(IReadOnlyList<DictationHotkey> bindings)
    {
        Bindings = bindings;
        BindingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDictationActive(bool active) => DictationActive = active;

    public void SimulateStart() => DictationStartRequested?.Invoke(this, EventArgs.Empty);
    public void SimulateStop() => DictationStopRequested?.Invoke(this, EventArgs.Empty);
    public void SimulateCancel() => DictationCancelRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}
