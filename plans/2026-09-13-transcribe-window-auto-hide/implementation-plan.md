# Implementation plan — Transcribe widget auto-hide

Requirements and decisions: [task.md](task.md). Comparison and rationale:
[research.md](research.md).

Everything here is inside `Parlotype.Desktop` + `Parlotype.Desktop.Tests`. Core and
Platform are untouched, and no resx key changes.

## The state machine, in one place

```
                       ShowTranscribeForDictation()      ShowTranscribe()  (tray / relaunch / onboarding)
                                  │                              │
              already visible? ───┤ yes → keep ownership          │
                                  │ no                            │
                                  ▼                               ▼
                            ┌───────────┐   click / drag /  ┌──────────┐
                            │ Dictation │ ──── flyout ────▶ │   User   │
                            └─────┬─────┘                   └──────────┘
                                  │                          never hides
        ┌─────────────────────────┴──────────────────────────┐
        │ armed only while ALL are true:                      │
        │   • !vm.IsDictationBusy   (settled)                 │
        │   • !vm.IsInErrorState    (clean outcome, FR-8)     │
        │   • !window.IsPointerOver (hover pauses, FR-6)      │
        └─────────────────────────┬──────────────────────────┘
                                  │ 1.5 s elapses
                                  ▼
                        HideWithFadeAsync()  ── 160 ms ──▶  Hide()
                                  │
                    any of the three flips back → abort fade, restore opacity
```

Hiding (by any route) resets ownership to `None`.

## Step 1 — `TranscribeViewModel`: a truthful "busy" signal

[src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs](../../src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs)

The controller needs two properties, and neither exists yet.

**`IsDictationBusy`** — true from the instant a start is requested until the last
character has been typed:

```csharp
/// <summary>
/// True while a dictation session still has work in flight: a start being
/// awaited, live recording, a model load, the post-stop drain, or a paste that
/// has not finished. Deliberately *not* just IsRecording — StopAsync returns
/// once the pipeline has drained, but OnTranscriptionAvailable is async void
/// and the clipboard round trip outlives it, so a window hidden on IsRecording
/// would disappear mid-paste.
/// </summary>
public bool IsDictationBusy =>
    _startTask is not null
    || IsRecording
    || RecordingState == RecordingState.Loading
    || Volatile.Read(ref _injectionsInFlight) > 0;
```

Change notification, all four sources:

- `_isRecording` and `_recordingState` gain `[NotifyPropertyChangedFor(nameof(IsDictationBusy))]`.
- `StartRecordingAsync` raises it manually where it assigns and clears `_startTask`
  (both already on the UI thread).
- A new `_injectionsInFlight` counter, incremented/decremented in
  `OnTranscriptionAvailable`. That handler runs on the **pipeline's background task**, so
  use `Interlocked.Increment/Decrement` for the counter and
  `Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(IsDictationBusy)))` for the
  notification.

```csharp
private async void OnTranscriptionAvailable(object? sender, TranscriptionEventArgs e)
{
    if (_textInjectionService is null || string.IsNullOrWhiteSpace(e.Result.Text))
        return;

    Interlocked.Increment(ref _injectionsInFlight);
    NotifyBusyChanged();
    try
    {
        await _textInjectionService.InjectTextAsync(e.Result.Text);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to inject transcribed text");
    }
    finally
    {
        Interlocked.Decrement(ref _injectionsInFlight);
        NotifyBusyChanged();
    }
}
```

**`IsInErrorState`** — derived from the `StatusKind` the VM already tracks, so nothing new
has to be stored:

```csharp
/// <summary>
/// The last session ended in a state the user probably needs to see (FR-8) —
/// the widget stays put instead of auto-hiding. Cleared by the next SetStatus,
/// so the next clean dictation lets it hide again.
/// </summary>
public bool IsInErrorState => _statusKind is
    StatusKind.RuntimeRestartRequired or StatusKind.RuntimeUnavailable
    or StatusKind.CloudNotConfigured or StatusKind.CloudKeyRejected
    or StatusKind.CloudQuotaExceeded or StatusKind.CloudRateLimited
    or StatusKind.CloudProviderUnavailable or StatusKind.CloudFailed;
```

`SetStatus` gains one line: `OnPropertyChanged(nameof(IsInErrorState));`.

Note `StatusKind.Cancelled` is deliberately absent — a cancelled take is a clean end
(FR-9).

## Step 2 — `TranscribeWindow`: engagement signals and the fade

[src/Parlotype.Desktop/Views/TranscribeWindow.axaml.cs](../../src/Parlotype.Desktop/Views/TranscribeWindow.axaml.cs)
and [.axaml](../../src/Parlotype.Desktop/Views/TranscribeWindow.axaml)

**AXAML** — one `Transitions` block on the existing `RootChrome` border:

```xml
<Border Name="RootChrome" ...>
    <Border.Transitions>
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.16" />
        </Transitions>
    </Border.Transitions>
```

Fading the inner card rather than `Window.Opacity`: the window is already
`TransparencyLevelHint="Transparent"` with a transparent background, the card *is* the
visible surface, and the codebase already animates a child `Border`'s opacity this way
(the record button). Avoids betting on layered-window opacity behaving identically across
the Windows compositor and the headless platform.

**Code-behind:**

```csharp
/// <summary>Raised when the user does something committal to the widget — a click
/// anywhere (which includes the grip drag and every button) or opening the language
/// flyout. Promotes an auto-summoned window to one the user owns (FR-5).</summary>
public event EventHandler? UserEngaged;

public TimeSpan FadeDuration { get; set; } = TimeSpan.FromMilliseconds(160);
```

- In the constructor, after `InitializeComponent()`:
  `AddHandler(InputElement.PointerPressedEvent, (_, _) => UserEngaged?.Invoke(this, EventArgs.Empty), RoutingStrategies.Tunnel);`
  Tunnel so it fires before any child handles or marks the event handled — the grip's own
  `GripZone_PointerPressed` and every button still work untouched.
- Cache `RootChrome` via `this.FindControl<Border>("RootChrome")`.

```csharp
/// <summary>
/// Hides the widget with a short fade (FR-10). Input is disabled for the duration
/// so a card on its way out cannot swallow a click. Abortable: a dictation that
/// restarts mid-fade calls AbortFade() and the card is restored, not hidden.
/// </summary>
public async Task HideWithFadeAsync()
{
    if (!IsVisible || _fade is not null)
        return;

    var fade = new CancellationTokenSource();
    _fade = fade;
    try
    {
        IsHitTestVisible = false;
        if (_rootChrome is not null)
            _rootChrome.Opacity = 0;

        await Task.Delay(FadeDuration, fade.Token);
        HideToTray();
    }
    catch (OperationCanceledException)
    {
        // aborted — fall through to the restore below
    }
    finally
    {
        _fade = null;
        fade.Dispose();
        RestoreChrome();
    }
}

/// <summary>Cancels an in-flight fade and leaves the window visible.</summary>
public void AbortFade() => _fade?.Cancel();

private void RestoreChrome()
{
    IsHitTestVisible = true;
    if (_rootChrome is not null)
        _rootChrome.Opacity = 1;
}
```

`HideToTray()` already saves the position, so the auto-hide inherits position persistence
for free. `RestoreChrome()` in the `finally` covers both outcomes, so the next `Show()`
always gets a fully opaque card — but belt-and-braces, `WindowManager.ShowTranscribe`
calls `RestoreChrome()` (made internal/public) before every `Show()` too. Transitions do
not animate under `Avalonia.Headless`; the property set and the delay still happen, so the
tests assert the end state, never a frame mid-fade.

## Step 3 — `TranscribeAutoHideController`

New file: `src/Parlotype.Desktop/Services/TranscribeAutoHideController.cs`

```csharp
internal sealed class TranscribeAutoHideController : IDisposable
{
    private enum Ownership { None, Dictation, User }

    public TranscribeAutoHideController(
        TranscribeWindow window,
        TranscribeViewModel viewModel,
        ILogger<TranscribeAutoHideController> logger);

    /// <summary>Grace period between a session settling and the window fading out.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>Injectable so tests drive the countdown instead of sleeping (NFR-3).</summary>
    public Func<TimeSpan, CancellationToken, Task> DelayProvider { get; set; } = Task.Delay;

    /// <summary>Called by WindowManager immediately before Show().</summary>
    public void NotifyShowing(bool byDictation);
}
```

Deliberately an `async`/`CancellationTokenSource` countdown rather than a
`DispatcherTimer`: a `DelayProvider` seam makes every test deterministic, where a real
timer would mean sleeping and pumping the dispatcher.

Subscriptions, all made in the constructor and released in `Dispose`:

| Source | Handler |
|---|---|
| `viewModel.PropertyChanged` (`IsDictationBusy`, `IsInErrorState`) | `Evaluate()` |
| `viewModel.PropertyChanged` (`IsLanguageFlyoutOpen` → true) | `Engage()` |
| `window.UserEngaged` | `Engage()` |
| `window.PointerEntered` / `PointerExited` (or `GetObservable(InputElement.IsPointerOverProperty)`) | `Evaluate()` |
| `window.GetObservable(Visual.IsVisibleProperty)` → false | `Reset()` |

```csharp
private void Evaluate()
{
    if (_ownership != Ownership.Dictation
        || _viewModel.IsDictationBusy
        || _viewModel.IsInErrorState
        || _window.IsPointerOver)
    {
        CancelCountdown();          // also calls _window.AbortFade()
        return;
    }

    StartCountdownIfIdle();         // no-op when one is already running
}
```

`NotifyShowing(byDictation)` keeps existing ownership when the window is already visible
(FR-4), otherwise sets `Dictation` or `User`. `Engage()` upgrades `Dictation` → `User` and
cancels any countdown or fade. `Reset()` (on hide) clears ownership to `None` so the next
summon decides afresh.

**Explicitly rejected trigger: `Window.Activated`.** Keyboard focus looks like the obvious
third engagement signal, but activation is not reliable here — the dictation path shows
the window with `ShowActivated = false`, and if Windows activates it anyway every
auto-summoned window would immediately become sticky and the whole feature would silently
do nothing. Pointer-press plus the flyout cover every way a user actually touches a
172×112 card, and `Esc` (the one keyboard interaction) already hides it. This is the first
thing to check in the manual pass.

## Step 4 — Wiring

[IWindowManager.cs](../../src/Parlotype.Desktop/Services/IWindowManager.cs) gains one method
rather than a parameter on `ShowTranscribe` — the two are different operations, and this
leaves the four existing call sites untouched:

```csharp
/// <summary>
/// Show the Transcribe window as the transient HUD for a dictation gesture:
/// without focus, and set to hide itself once the session settles (ADR-068).
/// A window the user already opened keeps its own lifetime.
/// </summary>
void ShowTranscribeForDictation();
```

[WindowManager.cs](../../src/Parlotype.Desktop/Services/WindowManager.cs):

- Extract the create-if-needed body of `ShowTranscribe` into a private
  `ShowCoreAsync(bool activate, bool byDictation)`; both public methods call it.
- Construct `TranscribeAutoHideController` alongside the window, in the same `if` block
  that builds it (it is a per-window object, so it belongs to the window's lifetime, not
  to DI).
- Before every `Show()`: `_transcribe.RestoreChrome()` then
  `_autoHide.NotifyShowing(byDictation)` — in that order, so a window mid-fade is restored
  before ownership is decided.
- `HideTranscribe()` stays as-is (still no production caller; now genuinely redundant, but
  removing public API is not this change's business).

[HotkeyCoordinator.cs:78](../../src/Parlotype.Desktop/Services/HotkeyCoordinator.cs:78):
`_windowManager.ShowTranscribe(activate: false)` → `_windowManager.ShowTranscribeForDictation()`.

Three stub implementations need the new method (all one-liners):
`Mocks/MockWindowManager.cs` (add a `ShowTranscribeForDictationCount`),
`AppViewModel.cs:59` and `TranscribeViewModel.cs:884` (the two nested `DesignWindowManager`
classes).

## Step 5 — Tests

`src/Parlotype.Desktop.Tests/TranscribeWindowAutoHideTests.cs` (new, `[AvaloniaFact]`,
`DelayProvider` wired to a `TaskCompletionSource` so every countdown is stepped by hand):

1. Dictation-summoned + settled + countdown elapses → `IsVisible == false`.
2. Tray-summoned (`ShowTranscribe`) + a full dictation session → still visible.
3. Visible and user-owned before dictation starts → still visible afterwards (FR-4).
4. Busy (`IsDictationBusy == true`) → no countdown starts at all.
5. Pointer over the window when it settles → no countdown; pointer leaves → countdown
   starts (FR-6).
6. `PointerPressed` on the window during the countdown → promoted to sticky, never hides
   (FR-5), and a later settle does not re-arm it.
7. `IsLanguageFlyoutOpen = true` during the countdown → same.
8. Busy again before the countdown elapses → pending hide cancelled (FR-7).
9. `IsInErrorState` after a failed start → no auto-hide (FR-8); a following clean session
   does hide it (self-heal).
10. Cancelled session (`StatusKind.Cancelled`) → hides (FR-9).
11. After an auto-hide, re-showing leaves `RootChrome.Opacity == 1` (FR-10).
12. `AbortFade()` mid-fade → window still visible, opacity restored.

`TranscribeViewModelTests.cs` (extend):

13. `IsDictationBusy` is true while `MockTextInjectionService` is still awaiting, and false
    once it completes — the regression guard for the mid-paste hide.
14. `IsDictationBusy` is true from `StartRecordingAsync` being called through to the stop
    completing.
15. `IsInErrorState` flips on for a cloud-not-configured start and clears on the next
    successful one.

`MockTextInjectionService` needs a gate (a `TaskCompletionSource` the test releases) for
(13); check whether it already has one before adding.

`TranscribeWindowChromeTests` and `TranscribeWindowScreenshotTests` should keep passing
untouched — confirm the new transition on `RootChrome` does not change any rendered
scenario.

## Step 6 — Documentation

- **ADR-068** `docs/decisions/068-transcribe-window-auto-hide.md`. Triggered by the
  `IWindowManager` surface change, a new Desktop service, and a behaviour change in the
  hotkey → window path. It amends ADR-040's "Hide, don't close" section: hiding is still
  never closing, but it is no longer always manual. Record the four decisions from
  task.md, the ownership rule, and the rejected `Window.Activated` trigger.
- Annotate [ADR-040](../../docs/decisions/040-frameless-compact-transcribe-window.md) with
  a pointer to ADR-068 next to its "Hide, don't close" heading.
- Vault: `memory/services/desktop.md` (new controller, the two new VM properties, the new
  `IWindowManager` method), `memory/architecture/subsystems.md` (the widget-lifetime
  section), `memory/decisions/_index.md` (ADR-068 row).
- `memory/knowledge/` only if the manual pass turns up something non-derivable — the
  `ShowActivated` behaviour on Windows is the likely candidate.
- Session note per `.claude/skills/session-management/SKILL.md`.
- `plans/INDEX.md`: move the row Planned → In Progress on start, remove on completion.

## Risks and things to verify by hand

| Risk | Check |
|---|---|
| Windows activates the widget despite `ShowActivated = false`, so it is instantly "engaged" and never hides | First manual check. If it happens, the fix is to keep `Activated` out of the engagement set (already the plan) and confirm the pointer-press path is the only promoter. |
| `IsPointerOver` not set on a frameless transparent `Window` | Fall back to `PointerEntered`/`PointerExited` on `RootChrome`. Verify headlessly *and* on Windows — a transparent window's hit-testing is the part most likely to differ. |
| The fade visibly stutters, or the card flashes back to full opacity before hiding | The `finally` restores opacity *after* `HideToTray()`; confirm the order does not produce a one-frame flash of an opaque card. Swap to restoring on the next `Show()` only, if it does. |
| An accidental hotkey tap makes the widget flash for 1.5 s | Should not happen — ADR-047's tap/hold discrimination gates the start — but worth watching during the manual pass. |
| Cloud error path leaves a mute sticky card (status is tooltip-only, ADR-040) | Known and accepted (task.md "Out of scope"). Confirm the dialog still carries the message and the next clean dictation clears the card. |
| The onboarding tour's Transcribe steps break | Onboarding calls plain `ShowTranscribe`, so it is `User`-owned and unaffected — verify by running the tour. |
