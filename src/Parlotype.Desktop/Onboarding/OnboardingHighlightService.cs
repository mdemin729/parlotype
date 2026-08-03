using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// Applies onboarding highlights to a window (ADR-056): finds controls marked
/// with <see cref="OnboardingTarget.IdProperty"/> in the window's visual tree
/// and attaches an <see cref="OnboardingHighlight"/> adorner to each. Ids that
/// are not found or not yet visible stay pending and are retried on every
/// <see cref="Avalonia.Layout.Layoutable.LayoutUpdated"/> until the next
/// <see cref="Apply"/>/<see cref="Clear"/> — that covers content that
/// materializes after navigation (the Settings ContentControl swap) and
/// visibility flips (the language strip). Missing ids are silently skipped: a
/// step without its target still shows its text, just without a highlight.
/// UI-thread only; not thread-safe by design.
/// </summary>
public sealed class OnboardingHighlightService
{
    private readonly List<Control> _adorned = [];
    private readonly HashSet<string> _pending = [];
    private Window? _window;

    public void Apply(Window window, IReadOnlyList<string> targetIds)
    {
        Clear();

        if (targetIds.Count == 0)
            return;

        _window = window;
        foreach (var id in targetIds)
            _pending.Add(id);

        window.LayoutUpdated += OnLayoutUpdated;
        TryResolvePending();
    }

    public void Clear()
    {
        if (_window is not null)
        {
            _window.LayoutUpdated -= OnLayoutUpdated;
            _window = null;
        }

        foreach (var control in _adorned)
            AdornerLayer.SetAdorner(control, null);
        _adorned.Clear();
        _pending.Clear();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => TryResolvePending();

    private void TryResolvePending()
    {
        if (_window is null || _pending.Count == 0)
            return;

        foreach (var control in _window.GetVisualDescendants().OfType<Control>())
        {
            var id = OnboardingTarget.GetId(control);
            if (id is null || !_pending.Contains(id) || !control.IsEffectivelyVisible)
                continue;

            AdornerLayer.SetAdorner(control, new OnboardingHighlight());
            _adorned.Add(control);
            _pending.Remove(id);
            if (_pending.Count == 0)
                break;
        }

        if (_pending.Count == 0 && _window is not null)
        {
            _window.LayoutUpdated -= OnLayoutUpdated;
            _window = null;
        }
    }
}
