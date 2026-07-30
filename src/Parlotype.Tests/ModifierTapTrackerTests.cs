using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class ModifierTapTrackerTests
{
    private static ModifierTapTracker CtrlTracker(ModifierSide side = ModifierSide.Either) =>
        new(HotkeyGesture.ForDoubleTap(ModifierKey.Ctrl, side));

    private static HotkeyKeyEvent CtrlDown(long t, ModifierSide side = ModifierSide.Left) =>
        HotkeyKeyEvent.ModifierDown(ModifierKey.Ctrl, side, t);

    private static HotkeyKeyEvent CtrlUp(long t, ModifierSide side = ModifierSide.Left) =>
        HotkeyKeyEvent.ModifierUp(ModifierKey.Ctrl, side, t);

    [Fact]
    public void Two_Quick_Taps_Fire()
    {
        var tracker = CtrlTracker();

        Assert.False(tracker.Process(CtrlDown(0)));
        Assert.False(tracker.Process(CtrlUp(80)));
        Assert.False(tracker.Process(CtrlDown(200)));
        Assert.True(tracker.Process(CtrlUp(260)));
    }

    [Fact]
    public void Single_Tap_Does_Not_Fire()
    {
        var tracker = CtrlTracker();

        Assert.False(tracker.Process(CtrlDown(0)));
        Assert.False(tracker.Process(CtrlUp(80)));
    }

    [Fact]
    public void Second_Tap_After_DoubleTap_Window_Does_Not_Fire()
    {
        var tracker = CtrlTracker();

        tracker.Process(CtrlDown(0));
        tracker.Process(CtrlUp(80));

        // 80 -> 500 exceeds the 350 ms inter-tap window.
        tracker.Process(CtrlDown(450));
        Assert.False(tracker.Process(CtrlUp(500)));
    }

    [Fact]
    public void Slow_Press_Is_Not_A_Tap()
    {
        var tracker = CtrlTracker();

        tracker.Process(CtrlDown(0));
        tracker.Process(CtrlUp(400)); // held well past TapMaxMs

        tracker.Process(CtrlDown(500));
        Assert.False(tracker.Process(CtrlUp(550)));
    }

    [Fact]
    public void CtrlC_Then_CtrlV_Does_Not_Read_As_DoubleTap()
    {
        // The regression this tracker exists to prevent.
        var tracker = CtrlTracker();

        tracker.Process(CtrlDown(0));
        tracker.Process(HotkeyKeyEvent.KeyDown("C", 40, HotkeyModifiers.Ctrl));
        tracker.Process(HotkeyKeyEvent.KeyUp("C", 80, HotkeyModifiers.Ctrl));
        tracker.Process(CtrlUp(120));

        tracker.Process(CtrlDown(200));
        tracker.Process(HotkeyKeyEvent.KeyDown("V", 240, HotkeyModifiers.Ctrl));
        tracker.Process(HotkeyKeyEvent.KeyUp("V", 280, HotkeyModifiers.Ctrl));

        Assert.False(tracker.Process(CtrlUp(320)));
    }

    [Fact]
    public void Intervening_Key_Invalidates_Only_That_Tap()
    {
        var tracker = CtrlTracker();

        // Ctrl+A — not a tap.
        tracker.Process(CtrlDown(0));
        tracker.Process(HotkeyKeyEvent.KeyDown("A", 30, HotkeyModifiers.Ctrl));
        tracker.Process(CtrlUp(60));

        // Two clean taps afterwards still work.
        tracker.Process(CtrlDown(200));
        Assert.False(tracker.Process(CtrlUp(250)));
        tracker.Process(CtrlDown(350));
        Assert.True(tracker.Process(CtrlUp(400)));
    }

    [Fact]
    public void Left_Then_Right_Ctrl_Is_Not_A_DoubleTap()
    {
        // "Either side" still means the same physical key twice.
        var tracker = CtrlTracker(ModifierSide.Either);

        tracker.Process(CtrlDown(0, ModifierSide.Left));
        tracker.Process(CtrlUp(60, ModifierSide.Left));
        tracker.Process(CtrlDown(150, ModifierSide.Right));

        Assert.False(tracker.Process(CtrlUp(200, ModifierSide.Right)));
    }

    [Fact]
    public void Right_Side_Taps_Fire_For_Either_Binding()
    {
        var tracker = CtrlTracker(ModifierSide.Either);

        tracker.Process(CtrlDown(0, ModifierSide.Right));
        tracker.Process(CtrlUp(60, ModifierSide.Right));
        tracker.Process(CtrlDown(150, ModifierSide.Right));

        Assert.True(tracker.Process(CtrlUp(200, ModifierSide.Right)));
    }

    [Fact]
    public void Side_Specific_Binding_Ignores_Other_Side()
    {
        var tracker = CtrlTracker(ModifierSide.Right);

        tracker.Process(CtrlDown(0, ModifierSide.Left));
        tracker.Process(CtrlUp(60, ModifierSide.Left));
        tracker.Process(CtrlDown(150, ModifierSide.Left));

        Assert.False(tracker.Process(CtrlUp(200, ModifierSide.Left)));
    }

    [Fact]
    public void Different_Modifier_Is_Ignored()
    {
        var tracker = CtrlTracker();

        tracker.Process(HotkeyKeyEvent.ModifierDown(ModifierKey.Shift, ModifierSide.Left, 0));
        tracker.Process(HotkeyKeyEvent.ModifierUp(ModifierKey.Shift, ModifierSide.Left, 60));
        tracker.Process(HotkeyKeyEvent.ModifierDown(ModifierKey.Shift, ModifierSide.Left, 150));

        Assert.False(tracker.Process(HotkeyKeyEvent.ModifierUp(ModifierKey.Shift, ModifierSide.Left, 200)));
    }

    [Fact]
    public void Triple_Tap_Fires_Once()
    {
        var tracker = CtrlTracker();

        tracker.Process(CtrlDown(0));
        tracker.Process(CtrlUp(50));
        tracker.Process(CtrlDown(150));
        Assert.True(tracker.Process(CtrlUp(200)));

        // Third tap starts a fresh pair rather than firing again.
        tracker.Process(CtrlDown(300));
        Assert.False(tracker.Process(CtrlUp(350)));
    }

    [Fact]
    public void Autorepeat_Down_Does_Not_Restart_The_Tap()
    {
        var tracker = CtrlTracker();

        tracker.Process(CtrlDown(0));
        tracker.Process(CtrlDown(100)); // auto-repeat while held
        tracker.Process(CtrlDown(200));

        // Release lands 300 ms after the original press — too slow for a tap.
        Assert.False(tracker.Process(CtrlUp(300)));
    }
}
