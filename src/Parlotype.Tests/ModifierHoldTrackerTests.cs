using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class ModifierHoldTrackerTests
{
    private static ModifierHoldTracker RightCtrlTracker(bool deferStart = false) =>
        new(HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right), deferStart);

    private static HotkeyKeyEvent CtrlDown(long t, ModifierSide side = ModifierSide.Right) =>
        HotkeyKeyEvent.ModifierDown(ModifierKey.Ctrl, side, t);

    private static HotkeyKeyEvent CtrlUp(long t, ModifierSide side = ModifierSide.Right) =>
        HotkeyKeyEvent.ModifierUp(ModifierKey.Ctrl, side, t);

    [Fact]
    public void Hold_Starts_On_KeyDown_And_Stops_On_Release()
    {
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(3000)));
    }

    [Fact]
    public void Release_Within_Tap_Window_Aborts_Instead_Of_Stopping()
    {
        // A quick tap is not speech; the audio is discarded so a double-tap
        // binding on the same key can own the gesture.
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.Aborted, tracker.Process(CtrlUp(90)));
    }

    [Fact]
    public void Key_Pressed_Within_Grace_Window_Aborts()
    {
        // Right Ctrl+C is a copy shortcut, not a dictation request.
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.Aborted,
            tracker.Process(HotkeyKeyEvent.KeyDown("C", 120, HotkeyModifiers.Ctrl)));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlUp(300)));
    }

    [Fact]
    public void Key_Pressed_After_Grace_Window_Does_Not_Abort()
    {
        // Users do type while dictating.
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.None,
            tracker.Process(HotkeyKeyEvent.KeyDown("C", 900, HotkeyModifiers.Ctrl)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(3000)));
    }

    [Fact]
    public void Other_Modifier_Does_Not_Abort()
    {
        var tracker = RightCtrlTracker();

        tracker.Process(CtrlDown(0));
        Assert.Equal(HoldOutcome.None,
            tracker.Process(HotkeyKeyEvent.ModifierDown(ModifierKey.Shift, ModifierSide.Left, 50)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(3000)));
    }

    [Fact]
    public void Left_Ctrl_Does_Not_Trigger_A_Right_Ctrl_Binding()
    {
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlDown(0, ModifierSide.Left)));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlUp(3000, ModifierSide.Left)));
    }

    [Fact]
    public void Autorepeat_Does_Not_Restart_The_Hold()
    {
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlDown(500)));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlDown(1000)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(3000)));
    }

    [Fact]
    public void Release_Without_Press_Is_Ignored()
    {
        var tracker = RightCtrlTracker();
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlUp(100)));
    }

    [Fact]
    public void Consecutive_Holds_Work()
    {
        var tracker = RightCtrlTracker();

        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(2000)));
        Assert.Equal(HoldOutcome.Started, tracker.Process(CtrlDown(2500)));
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(5000)));
    }

    // --- deferred start (a double-tap binding shares the key) ---

    [Fact]
    public void Deferred_Hold_Does_Not_Start_On_KeyDown()
    {
        var tracker = RightCtrlTracker(deferStart: true);

        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlDown(0)));
        Assert.Equal(HotkeyGestureTiming.TapMaxMs, tracker.PendingTimeoutMs);
    }

    [Fact]
    public void Deferred_Hold_Starts_Once_The_Tap_Window_Elapses()
    {
        var tracker = RightCtrlTracker(deferStart: true);

        tracker.Process(CtrlDown(0));
        Assert.Equal(HoldOutcome.None, tracker.ProcessTimeout(100));   // too early
        Assert.Equal(HoldOutcome.Started, tracker.ProcessTimeout(250));
        Assert.Null(tracker.PendingTimeoutMs);
        Assert.Equal(HoldOutcome.Stopped, tracker.Process(CtrlUp(3000)));
    }

    [Fact]
    public void Deferred_Hold_Released_Early_Emits_Nothing()
    {
        // This is the double-tap path: no recording ever started, so there is
        // nothing to flicker on screen and nothing to discard.
        var tracker = RightCtrlTracker(deferStart: true);

        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlDown(0)));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlUp(90)));
        Assert.Null(tracker.PendingTimeoutMs);
    }

    [Fact]
    public void Deferred_Hold_Aborted_Before_Start_Emits_Nothing()
    {
        var tracker = RightCtrlTracker(deferStart: true);

        tracker.Process(CtrlDown(0));
        Assert.Equal(HoldOutcome.None,
            tracker.Process(HotkeyKeyEvent.KeyDown("C", 100, HotkeyModifiers.Ctrl)));
        Assert.Null(tracker.PendingTimeoutMs);
        Assert.Equal(HoldOutcome.None, tracker.ProcessTimeout(400));
        Assert.Equal(HoldOutcome.None, tracker.Process(CtrlUp(500)));
    }
}
