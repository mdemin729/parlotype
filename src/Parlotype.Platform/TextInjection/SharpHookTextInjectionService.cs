using Microsoft.Extensions.Logging;
using Parlotype.Core.TextInjection;
using SharpHook;
using SharpHook.Data;

namespace Parlotype.Platform.TextInjection;

/// <summary>
/// Injects text using SharpHook's <see cref="EventSimulator.SimulateTextEntry"/> which
/// simulates Unicode character input via the OS input system.
/// </summary>
public sealed class SharpHookTextInjectionService : ITextInjectionService
{
    private readonly ITargetWindowTracker _windowTracker;
    private readonly ILogger<SharpHookTextInjectionService> _logger;
    private readonly EventSimulator _simulator = new();

    public SharpHookTextInjectionService(
        ITargetWindowTracker windowTracker,
        ILogger<SharpHookTextInjectionService> logger)
    {
        _windowTracker = windowTracker;
        _logger = logger;
    }

    public async Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _windowTracker.ActivateTargetWindow();

        // Brief delay to let the target window fully activate
        await Task.Delay(50, cancellationToken);

        var result = _simulator.SimulateTextEntry(text);

        if (result != UioHookResult.Success)
        {
            _logger.LogError("SimulateTextEntry failed with {Result}", result);
            throw new InvalidOperationException($"SharpHook text simulation failed: {result}");
        }

        _logger.LogDebug("Injected {Length} characters via SharpHook", text.Length);
    }
}
