using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Owns the "whoever summoned the window decides when it leaves" rule
/// (ADR-068): a Transcribe window summoned by a dictation gesture is
/// transient and fades itself out once the session settles; a window the
/// user opened themselves — tray click, tray "Open", a second launch, the
/// onboarding tour — is sticky and never auto-hides. One instance per
/// <see cref="TranscribeWindow"/>, constructed alongside it by
/// <see cref="WindowManager"/> and living for the window's lifetime — it is
/// not registered in DI because it has no meaning independent of the window
/// it watches.
/// </summary>
internal sealed class TranscribeAutoHideController : IDisposable
{
    /// <summary>Who currently owns the window's visible lifetime. <see cref="None"/>
    /// only between a hide and the next summon — <see cref="NotifyShowing"/>
    /// always leaves it as one of the other two.</summary>
    private enum Ownership
    {
        None,
        Dictation,
        User,
    }

    private readonly TranscribeWindow _window;
    private readonly TranscribeViewModel _viewModel;
    private readonly ILogger<TranscribeAutoHideController> _logger;

    private Ownership _ownership = Ownership.None;

    /// <summary>The in-flight settle-to-hide countdown, if any. Non-null exactly
    /// while a countdown is pending or its <see cref="TranscribeWindow.HideWithFadeAsync"/>
    /// call is under way — cleared either by <see cref="CancelCountdown"/> (a
    /// condition stopped holding) or by <see cref="Reset"/> once the window
    /// actually hides.</summary>
    private CancellationTokenSource? _countdown;

    /// <summary>Grace period between a session settling and the window fading
    /// out (ADR-068). Long enough to register the widget finishing; short
    /// enough that it is gone before attention returns to the text that just
    /// appeared. Hover pauses it (FR-6), so precision does not matter.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// Injectable so tests drive the countdown by hand instead of sleeping for
    /// real seconds (NFR-3) — the same seam
    /// <see cref="TranscribeViewModel.LoadingSpinnerDelay"/> uses for its own
    /// timing. Defaults to the real <see cref="Task.Delay(TimeSpan,CancellationToken)"/>.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task> DelayProvider { get; set; } = Task.Delay;

    public TranscribeAutoHideController(
        TranscribeWindow window,
        TranscribeViewModel viewModel,
        ILogger<TranscribeAutoHideController> logger)
    {
        _window = window;
        _viewModel = viewModel;
        _logger = logger;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _window.UserEngaged += OnUserEngaged;

        // A single AvaloniaObject.PropertyChanged subscription covers both
        // signals below rather than two GetObservable(...).Subscribe(...)
        // calls — IObservable<T> itself declares Subscribe(IObserver<T>), so
        // the Action<T>-taking extension overload is never considered when
        // called dot-style, and the extension's declaring type is internal to
        // Avalonia.Base, so it cannot be called qualified from here either.
        _window.PropertyChanged += OnWindowPropertyChanged;
    }

    /// <summary>
    /// Called by <see cref="WindowManager"/> immediately before every
    /// <c>Show()</c>. A window already visible keeps whoever owns it now
    /// (FR-4) — a dictation gesture cannot take away a window the user
    /// opened, and re-summoning an already-transient window does not reset
    /// its ownership either. Otherwise ownership is decided fresh from how
    /// this call was made. Always re-evaluates afterwards: a summon with
    /// nothing to wait for (no pipeline wired, or a session that was already
    /// settled) must still be able to start the countdown, not just changes
    /// to <see cref="TranscribeViewModel.IsDictationBusy"/> after the fact.
    /// </summary>
    public void NotifyShowing(bool byDictation)
    {
        if (!_window.IsVisible)
            _ownership = byDictation ? Ownership.Dictation : Ownership.User;

        Evaluate();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TranscribeViewModel.IsDictationBusy):
            case nameof(TranscribeViewModel.IsInErrorState):
                Evaluate();
                break;

            // Opening the flyout is committal the same way a click is — only
            // opening (not closing) counts, so dismissing it does not by
            // itself re-arm a countdown that engagement already cancelled.
            case nameof(TranscribeViewModel.IsLanguageFlyoutOpen):
                if (_viewModel.IsLanguageFlyoutOpen)
                    Engage();
                break;
        }
    }

    private void OnUserEngaged(object? sender, EventArgs e) => Engage();

    /// <summary>
    /// Watches the two window properties the countdown depends on.
    /// <see cref="InputElement.IsPointerOverProperty"/> backs the hover pause
    /// (FR-6) — every change re-evaluates, so leaving restarts a full
    /// countdown rather than resuming a partial one (research.md: simpler to
    /// reason about, and the pause makes exact duration not worth chasing).
    /// <see cref="Visual.IsVisibleProperty"/> going false is the one reliable
    /// "the window is gone" signal — it covers the ✕ button, Esc, and this
    /// controller's own auto-hide fade alike, so ownership resets to
    /// <see cref="Ownership.None"/> exactly once regardless of which route
    /// hid the window.
    /// </summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == InputElement.IsPointerOverProperty)
            Evaluate();
        else if (e.Property == Visual.IsVisibleProperty && e.NewValue is false)
            Reset();
    }

    /// <summary>
    /// Promotes a transient window to sticky for the rest of its visible life
    /// (FR-5). A no-op once the window is already <see cref="Ownership.User"/>
    /// or was never dictation-owned to begin with — engagement only ever
    /// moves ownership one way.
    /// </summary>
    private void Engage()
    {
        if (_ownership != Ownership.Dictation)
            return;

        _ownership = Ownership.User;
        CancelCountdown();
    }

    /// <summary>The window actually hid — by ✕, Esc, or this controller's own
    /// fade. Ownership resets to None so the next summon decides afresh.</summary>
    private void Reset()
    {
        _ownership = Ownership.None;
        CancelCountdown();
    }

    /// <summary>
    /// The state machine's single decision point (ADR-068): armed only while
    /// all three hold. Anything else — busy, an error outcome (FR-8), or the
    /// pointer sitting over the card (FR-6) — cancels whatever is pending.
    /// Deliberately excludes <see cref="Avalonia.Controls.Window.Activated"/>:
    /// the dictation path shows the window with <c>ShowActivated = false</c>,
    /// but if the OS activates it anyway regardless, treating activation as
    /// engagement would make every auto-summoned window instantly sticky and
    /// silently disable the whole feature. Pointer-press and the flyout cover
    /// every way a user actually touches a 172x112 card.
    /// </summary>
    private void Evaluate()
    {
        if (_ownership != Ownership.Dictation
            || _viewModel.IsDictationBusy
            || _viewModel.IsInErrorState
            || _window.IsPointerOver)
        {
            CancelCountdown();
            return;
        }

        StartCountdownIfIdle();
    }

    private void StartCountdownIfIdle()
    {
        if (_countdown is not null)
            return; // Already counting down (or already fading — see CancelCountdown).

        var cts = new CancellationTokenSource();
        _countdown = cts;
        _ = RunCountdownAsync(cts);
    }

    private async Task RunCountdownAsync(CancellationTokenSource cts)
    {
        try
        {
            await DelayProvider(Delay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // Evaluate()/Engage() found a reason not to hide any more.
        }

        try
        {
            await _window.HideWithFadeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-hide the Transcribe window");
        }
    }

    /// <summary>
    /// Cancels a pending countdown and/or an in-flight fade — the fade must
    /// be aborted here too, not just the countdown, because the delay having
    /// elapsed does not clear <see cref="_countdown"/>; only <see cref="Reset"/>
    /// (on the window actually hiding) does. So a click or busy transition
    /// that lands mid-fade reaches <see cref="TranscribeWindow.AbortFade"/>
    /// through this same path.
    /// </summary>
    private void CancelCountdown()
    {
        if (_countdown is not null)
        {
            _countdown.Cancel();
            _countdown.Dispose();
            _countdown = null;
        }

        _window.AbortFade();
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _window.UserEngaged -= OnUserEngaged;
        _window.PropertyChanged -= OnWindowPropertyChanged;
        CancelCountdown();
    }
}
