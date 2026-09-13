using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The "whoever summoned the window decides when it leaves" state machine
/// (ADR-068): ownership, the settle-to-hide countdown, hover pause, engagement
/// promotion, error suppression, and the auto-hide fade. <see cref="ManualCountdown"/>
/// stands in for <see cref="TranscribeAutoHideController.DelayProvider"/> so no
/// test sleeps for the real 1.5s grace period (NFR-3); <see cref="TranscribeWindow.FadeDuration"/>
/// is shrunk to a millisecond so the (non-injectable) fade delay is negligible too.
/// </summary>
public class TranscribeWindowAutoHideTests
{
    /// <summary>
    /// Stands in for <see cref="TranscribeAutoHideController.DelayProvider"/>:
    /// records how many countdowns were started and lets a test resolve the
    /// pending one by hand instead of waiting out the real delay.
    /// </summary>
    private sealed class ManualCountdown
    {
        private TaskCompletionSource? _pending;

        /// <summary>How many times the controller has started a countdown.</summary>
        public int StartCount { get; private set; }

        /// <summary>The most recently started countdown's task, if any — asserted
        /// against directly to confirm a cancellation actually reached it.</summary>
        public Task? Pending => _pending?.Task;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            StartCount++;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = tcs;
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        /// <summary>Simulates the grace period elapsing.</summary>
        public void Elapse() => _pending?.TrySetResult();
    }

    private static (TranscribeWindow Window, TranscribeViewModel Vm, MockAudioPipeline Pipeline,
        TranscribeAutoHideController Controller, ManualCountdown Delay) CreateHarness()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);
        var window = new TranscribeWindow
        {
            DataContext = vm,
            // Real (non-injectable) fade delay — shrunk to negligible per the
            // plan's own test guidance rather than left at the production 160ms.
            FadeDuration = TimeSpan.FromMilliseconds(1),
        };
        DisableChromeTransition(window);
        var delay = new ManualCountdown();
        var controller = new TranscribeAutoHideController(
            window, vm, NullLogger<TranscribeAutoHideController>.Instance)
        {
            DelayProvider = delay.Delay,
        };
        return (window, vm, pipeline, controller, delay);
    }

    /// <summary>
    /// Strips <c>RootChrome</c>'s AXAML-declared Opacity <c>DoubleTransition</c>
    /// (the ADR-068 fade). Avalonia.Headless still drives it on a real
    /// wall-clock timer even though it renders no frames, and a transition
    /// left ticking past a test's own return has been observed to fire during
    /// a later test's dispatcher pump and corrupt it (a
    /// "Cannot get KeyValueStorage on the idle test context" cascade across
    /// the whole file) — exactly the kind of full-run-only corruption this
    /// repo has a history of. Tests only ever need Opacity's synchronous
    /// end-state (per the plan's own guidance), so removing the transition
    /// up front — before anything ever touches Opacity — makes the property
    /// a plain, instantly-applied value with nothing left running afterwards.
    /// </summary>
    private static void DisableChromeTransition(TranscribeWindow window)
    {
        var chrome = window.FindControl<Border>("RootChrome");
        Assert.NotNull(chrome);
        chrome.Transitions = null;
    }

    /// <summary>Mirrors <c>WindowManager.ShowCoreAsync</c>'s exact call order
    /// (ADR-068): restore chrome, decide ownership, then show.</summary>
    private static void Summon(TranscribeWindow window, TranscribeAutoHideController controller, bool byDictation)
    {
        window.RestoreChrome();
        controller.NotifyShowing(byDictation);
        window.Show();
    }

    /// <summary>
    /// Disposes the controller, then pumps the shared headless dispatcher once
    /// more before the test returns. <see cref="TranscribeAutoHideController.Dispose"/>
    /// cancels any in-flight countdown, but cancellation only *schedules* the
    /// countdown task's continuation (the rest of its internal
    /// <c>RunCountdownAsync</c> loop, via the <see cref="ManualCountdown"/>'s
    /// <c>RunContinuationsAsynchronously</c> completion) onto the one
    /// process-wide dispatcher thread every <c>[AvaloniaFact]</c> test in the
    /// assembly shares — it does not necessarily run that continuation before
    /// <c>Dispose</c> returns. Left undrained, that stray continuation is
    /// still queued when the test method returns and can fire during some
    /// later, unrelated test's own dispatcher pump instead — the exact
    /// "leftover work corrupts a later test" hazard class documented in
    /// memory/knowledge/culture-changing-tests-need-avaloniafact.md, and the
    /// mechanism that made the full <c>Parlotype.Desktop.Tests</c> run hang
    /// under real machine contention despite every test disposing its own
    /// controller and closing its own window.
    /// </summary>
    private static async Task DisposeControllerAsync(TranscribeAutoHideController controller)
    {
        controller.Dispose();
        await Dispatcher.UIThread.InvokeAsync(() => { });
    }

    /// <summary>
    /// Polls for a condition that only becomes true after a real (but tiny)
    /// asynchronous hop — the fade's un-injectable <c>Task.Delay(FadeDuration)</c>
    /// — rather than sleeping a fixed guess. Bounded so a genuine regression
    /// fails the test instead of hanging it.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    // 1. Dictation-summoned + settled + countdown elapses → hides (FR-1, FR-2, FR-10).
    [AvaloniaFact]
    public async Task DictationSettled_CountdownElapses_HidesWindow()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);
        Assert.Equal(0, delay.StartCount); // still busy — must not arm mid-session

        await vm.StopRecordingAsync(); // session settles
        Assert.Equal(1, delay.StartCount);

        delay.Elapse();
        await WaitUntilAsync(() => !window.IsVisible);

        Assert.False(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 2. Tray-summoned (ShowTranscribe) + a full dictation session → still visible (FR-3).
    [AvaloniaFact]
    public async Task TraySummoned_FullDictationSession_StaysVisible()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        Summon(window, controller, byDictation: false); // tray/user summon — sticky from the start

        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        Assert.Equal(0, delay.StartCount);
        Assert.True(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 3. Visible and user-owned before dictation starts → still visible afterwards (FR-4).
    [AvaloniaFact]
    public async Task AlreadyVisibleUserOwned_DictationCannotTakeOwnership()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        Summon(window, controller, byDictation: false);
        Assert.True(window.IsVisible);

        // A dictation gesture re-summons the already-visible, user-owned window.
        Summon(window, controller, byDictation: true);

        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        Assert.Equal(0, delay.StartCount);
        Assert.True(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 4. Busy (IsDictationBusy == true) → no countdown starts at all (FR-2).
    [AvaloniaFact]
    public async Task Busy_NoCountdownStarts()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);

        Assert.True(vm.IsDictationBusy);
        Assert.Equal(0, delay.StartCount);
        Assert.True(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 5. Pointer over the window when it settles → no countdown; leaving restarts it (FR-6).
    [AvaloniaFact]
    public async Task PointerOverOnSettle_PausesCountdown_LeavingRestartsIt()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);

        window.MouseMove(new Point(10, 10), RawInputModifiers.None);
        Assert.True(window.IsPointerOver);

        await vm.StopRecordingAsync(); // settles while the pointer is still over the card
        Assert.Equal(0, delay.StartCount);

        window.MouseMove(new Point(500, 500), RawInputModifiers.None); // pointer leaves
        Assert.False(window.IsPointerOver);
        Assert.Equal(1, delay.StartCount); // leaving re-arms a full countdown

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 6. PointerPressed during the countdown → promoted to sticky, never hides again (FR-5).
    [AvaloniaFact]
    public async Task PointerPressedDuringCountdown_PromotesToSticky_NeverHidesAgain()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);
        await vm.StopRecordingAsync();
        Assert.Equal(1, delay.StartCount);

        window.MouseDown(new Point(10, 10), MouseButton.Left, RawInputModifiers.None);
        Assert.True(window.IsVisible); // promoted, not hidden

        // A later settle must not re-arm the countdown — the window is sticky now.
        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        Assert.Equal(1, delay.StartCount);
        Assert.True(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 7. IsLanguageFlyoutOpen = true during the countdown → same promotion as a click (FR-5).
    [AvaloniaFact]
    public async Task LanguageFlyoutOpenDuringCountdown_PromotesToSticky()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);
        await vm.StopRecordingAsync();
        Assert.Equal(1, delay.StartCount);

        vm.IsLanguageFlyoutOpen = true;
        Assert.True(window.IsVisible);

        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        Assert.Equal(1, delay.StartCount);
        Assert.True(window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 8. Busy again before the countdown elapses → pending hide cancelled (FR-7).
    [AvaloniaFact]
    public async Task BusyAgainDuringCountdown_CancelsPendingHide()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);
        await vm.StopRecordingAsync();
        Assert.Equal(1, delay.StartCount);
        var pending = delay.Pending;
        Assert.NotNull(pending);

        await vm.StartRecordingAsync(); // a new session starts mid-countdown

        Assert.True(pending.IsCanceled);
        Assert.True(window.IsVisible);

        await vm.StopRecordingAsync();
        Assert.Equal(2, delay.StartCount); // re-armed after settling again

        delay.Elapse();
        await WaitUntilAsync(() => !window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 9. IsInErrorState after a failed start → no auto-hide (FR-8); a following
    //    clean session hides it — the state self-heals.
    [AvaloniaFact]
    public async Task ErrorState_SuppressesAutoHide_SelfHealsOnCleanSession()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new CloudProviderNotConfiguredException(
                SpeechEngine.XaiGrok,
                CloudConfigurationError.MissingApiKey,
                "No API key configured for the xAI Grok provider."),
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);
        var window = new TranscribeWindow
        {
            DataContext = vm,
            FadeDuration = TimeSpan.FromMilliseconds(1),
        };
        DisableChromeTransition(window);
        var delay = new ManualCountdown();
        var controller = new TranscribeAutoHideController(
            window, vm, NullLogger<TranscribeAutoHideController>.Instance)
        {
            DelayProvider = delay.Delay,
        };

        await vm.StartRecordingAsync(); // fails synchronously → CloudNotConfigured
        Assert.True(vm.IsInErrorState);

        Summon(window, controller, byDictation: true);
        Assert.Equal(0, delay.StartCount); // error outcome suppresses the auto-hide

        // The next session is clean and settles normally — the card self-heals.
        pipeline.ThrowOnStart = null;
        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        Assert.False(vm.IsInErrorState);
        Assert.Equal(1, delay.StartCount);

        delay.Elapse();
        await WaitUntilAsync(() => !window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 10. Cancelled session (StatusKind.Cancelled) → hides (FR-9).
    [AvaloniaFact]
    public async Task CancelledSession_CountsAsCleanEnd_AutoHides()
    {
        var (window, vm, _, controller, delay) = CreateHarness();

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);

        await vm.CancelRecordingAsync(); // Esc-equivalent

        Assert.False(vm.IsInErrorState);
        Assert.Equal(1, delay.StartCount);

        delay.Elapse();
        await WaitUntilAsync(() => !window.IsVisible);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 11. After an auto-hide, re-showing leaves RootChrome.Opacity == 1 (FR-10).
    [AvaloniaFact]
    public async Task AfterAutoHide_ReshowingRestoresFullOpacity()
    {
        var (window, vm, _, controller, delay) = CreateHarness();
        var chrome = window.FindControl<Border>("RootChrome");
        Assert.NotNull(chrome);

        await vm.StartRecordingAsync();
        Summon(window, controller, byDictation: true);
        await vm.StopRecordingAsync();

        delay.Elapse();
        await WaitUntilAsync(() => !window.IsVisible);

        // HideWithFadeAsync's own finally already restored full opacity.
        Assert.Equal(1.0, chrome.Opacity);

        // Re-summon exactly as WindowManager does before every Show() — belt-and-braces
        // (ADR-068) — opacity must still read 1 on the fresh summon.
        Summon(window, controller, byDictation: true);
        Assert.True(window.IsVisible);
        Assert.Equal(1.0, chrome.Opacity);

        await DisposeControllerAsync(controller);
        window.Close();
    }

    // 12. AbortFade() mid-fade → window still visible, opacity restored.
    [AvaloniaFact]
    public async Task AbortFadeMidFade_LeavesWindowVisibleWithOpacityRestored()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());
        var window = new TranscribeWindow
        {
            DataContext = vm,
            // Long enough that the internal Task.Delay backing this field cannot
            // complete on its own before the test calls AbortFade() below — the
            // point is to abort a fade genuinely still in flight.
            FadeDuration = TimeSpan.FromSeconds(10),
        };
        DisableChromeTransition(window);
        window.Show();

        var chrome = window.FindControl<Border>("RootChrome");
        Assert.NotNull(chrome);

        var fadeTask = window.HideWithFadeAsync();
        // HideWithFadeAsync runs synchronously up to its internal await, so the
        // opacity drop and input lock are already applied here — no wait needed.
        Assert.Equal(0.0, chrome.Opacity);
        Assert.False(window.IsHitTestVisible);

        window.AbortFade();
        await fadeTask;

        Assert.True(window.IsVisible); // never actually hidden
        Assert.Equal(1.0, chrome.Opacity);
        Assert.True(window.IsHitTestVisible);

        window.Close();
    }
}
