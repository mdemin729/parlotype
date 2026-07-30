using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class HotkeyGestureMatcherTests
{
    private static HotkeyGestureMatcher Default() => new(DictationHotkeyDefaults.All);

    private static HotkeyKeyEvent RCtrlDown(long t) =>
        HotkeyKeyEvent.ModifierDown(ModifierKey.Ctrl, ModifierSide.Right, t, HotkeyModifiers.Ctrl);

    private static HotkeyKeyEvent RCtrlUp(long t) =>
        HotkeyKeyEvent.ModifierUp(ModifierKey.Ctrl, ModifierSide.Right, t);

    private static HotkeyKeyEvent LCtrlDown(long t) =>
        HotkeyKeyEvent.ModifierDown(ModifierKey.Ctrl, ModifierSide.Left, t, HotkeyModifiers.Ctrl);

    private static HotkeyKeyEvent LCtrlUp(long t) =>
        HotkeyKeyEvent.ModifierUp(ModifierKey.Ctrl, ModifierSide.Left, t);

    // --- push-to-talk: hold Right Ctrl ---

    [Fact]
    public void Holding_Right_Ctrl_Starts_After_The_Tap_Window_And_Stops_On_Release()
    {
        var matcher = Default();

        // Deferred, because the default set also binds double-tap Ctrl.
        Assert.Equal(DictationAction.None, matcher.Process(RCtrlDown(0)).Action);
        Assert.Equal(HotkeyGestureTiming.TapMaxMs, matcher.NextTimeoutMs);

        Assert.Equal(DictationAction.Start, matcher.ProcessTimeout(250).Action);
        Assert.Null(matcher.NextTimeoutMs);

        matcher.SetDictationActive(true);
        Assert.Equal(DictationAction.Stop, matcher.Process(RCtrlUp(3000)).Action);
    }

    [Fact]
    public void Hold_Starts_Immediately_When_No_DoubleTap_Shares_The_Key()
    {
        var matcher = new HotkeyGestureMatcher([DictationHotkeyDefaults.PushToTalk]);

        Assert.Equal(DictationAction.Start, matcher.Process(RCtrlDown(0)).Action);
        Assert.Null(matcher.NextTimeoutMs);
    }

    [Fact]
    public void Right_Ctrl_Plus_Letter_Aborts_The_Recording()
    {
        var matcher = new HotkeyGestureMatcher([DictationHotkeyDefaults.PushToTalk]);

        Assert.Equal(DictationAction.Start, matcher.Process(RCtrlDown(0)).Action);
        matcher.SetDictationActive(true);

        var result = matcher.Process(HotkeyKeyEvent.KeyDown("C", 100, HotkeyModifiers.Ctrl));
        Assert.Equal(DictationAction.Cancel, result.Action);
        Assert.False(result.Suppress); // the app still gets its Ctrl+C
    }

    [Fact]
    public void Right_Ctrl_Shortcut_At_Typing_Speed_Never_Starts_A_Recording()
    {
        // With the default set the hold is deferred, so an abort that lands
        // inside the tap window prevents the start outright — there is no
        // recording to flash on screen and none to discard.
        var matcher = Default();

        var results = new[]
        {
            matcher.Process(RCtrlDown(0)),
            matcher.Process(HotkeyKeyEvent.KeyDown("C", 100, HotkeyModifiers.Ctrl)),
            matcher.Process(HotkeyKeyEvent.KeyUp("C", 160, HotkeyModifiers.Ctrl)),
            matcher.Process(RCtrlUp(200)),
        };

        Assert.All(results, r => Assert.Equal(DictationAction.None, r.Action));
        Assert.Null(matcher.NextTimeoutMs);
        Assert.Equal(DictationAction.None, matcher.ProcessTimeout(500).Action);
    }

    [Fact]
    public void Typing_Well_Into_A_Hold_Does_Not_Abort_It()
    {
        // People do type while dictating; only keys pressed right at the start
        // suggest the hold was really a shortcut.
        var matcher = Default();

        matcher.Process(RCtrlDown(0));
        Assert.Equal(DictationAction.Start, matcher.ProcessTimeout(250).Action);
        matcher.SetDictationActive(true);

        Assert.Equal(DictationAction.None,
            matcher.Process(HotkeyKeyEvent.KeyDown("C", 900, HotkeyModifiers.Ctrl)).Action);
        Assert.Equal(DictationAction.Stop, matcher.Process(RCtrlUp(3000)).Action);
    }

    [Fact]
    public void Bare_Modifier_Events_Are_Never_Suppressed()
    {
        // Suppressing Ctrl would break every Ctrl shortcut on the machine.
        var matcher = Default();

        Assert.False(matcher.Process(RCtrlDown(0)).Suppress);
        Assert.False(matcher.ProcessTimeout(250).Suppress);
        Assert.False(matcher.Process(RCtrlUp(3000)).Suppress);
    }

    // --- toggle: double-tap Ctrl ---

    [Fact]
    public void DoubleTap_Ctrl_Toggles_On_Then_Off()
    {
        var matcher = Default();

        Assert.Equal(DictationAction.None, matcher.Process(RCtrlDown(0)).Action);
        Assert.Equal(DictationAction.None, matcher.Process(RCtrlUp(80)).Action);
        Assert.Equal(DictationAction.None, matcher.Process(RCtrlDown(200)).Action);
        Assert.Equal(DictationAction.Start, matcher.Process(RCtrlUp(260)).Action);

        matcher.SetDictationActive(true);

        matcher.Process(RCtrlDown(1000));
        matcher.Process(RCtrlUp(1060));
        matcher.Process(RCtrlDown(1150));
        Assert.Equal(DictationAction.Stop, matcher.Process(RCtrlUp(1200)).Action);
    }

    [Fact]
    public void DoubleTap_Emits_No_Spurious_Start_Or_Cancel_From_The_Hold_Binding()
    {
        // The reason hold-start is deferred: without it this sequence would be
        // Start, Cancel, Start, Cancel, Start and the widget would flicker.
        var matcher = Default();

        var actions = new[]
            {
                matcher.Process(RCtrlDown(0)),
                matcher.Process(RCtrlUp(80)),
                matcher.Process(RCtrlDown(200)),
                matcher.Process(RCtrlUp(260)),
            }
            .Select(r => r.Action)
            .Where(a => a != DictationAction.None)
            .ToArray();

        Assert.Equal([DictationAction.Start], actions);
    }

    [Fact]
    public void Left_Ctrl_DoubleTap_Also_Toggles()
    {
        var matcher = Default();

        matcher.Process(LCtrlDown(0));
        matcher.Process(LCtrlUp(60));
        matcher.Process(LCtrlDown(150));
        Assert.Equal(DictationAction.Start, matcher.Process(LCtrlUp(200)).Action);
    }

    [Fact]
    public void Ordinary_Ctrl_Shortcuts_Do_Not_Trigger_Anything()
    {
        var matcher = Default();

        var results = new List<HotkeyMatchResult>
        {
            matcher.Process(LCtrlDown(0)),
            matcher.Process(HotkeyKeyEvent.KeyDown("C", 40, HotkeyModifiers.Ctrl)),
            matcher.Process(HotkeyKeyEvent.KeyUp("C", 80, HotkeyModifiers.Ctrl)),
            matcher.Process(LCtrlUp(120)),
            matcher.Process(LCtrlDown(200)),
            matcher.Process(HotkeyKeyEvent.KeyDown("V", 240, HotkeyModifiers.Ctrl)),
            matcher.Process(HotkeyKeyEvent.KeyUp("V", 280, HotkeyModifiers.Ctrl)),
            matcher.Process(LCtrlUp(320)),
        };

        Assert.All(results, r => Assert.Equal(DictationAction.None, r.Action));
        Assert.All(results, r => Assert.False(r.Suppress));
    }

    // --- chord ---

    [Fact]
    public void Chord_Toggles_And_Is_Suppressed()
    {
        var matcher = Default();
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;

        var down = matcher.Process(HotkeyKeyEvent.KeyDown("Space", 100, mods));
        Assert.Equal(DictationAction.Start, down.Action);
        Assert.True(down.Suppress);

        var up = matcher.Process(HotkeyKeyEvent.KeyUp("Space", 150, mods));
        Assert.Equal(DictationAction.None, up.Action);
        Assert.True(up.Suppress); // no lone key-up reaches the target app
    }

    [Fact]
    public void Chord_Autorepeat_Does_Not_Toggle_Repeatedly()
    {
        var matcher = Default();
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;

        Assert.Equal(DictationAction.Start, matcher.Process(HotkeyKeyEvent.KeyDown("Space", 100, mods)).Action);
        matcher.SetDictationActive(true);

        var repeat = matcher.Process(HotkeyKeyEvent.KeyDown("Space", 140, mods));
        Assert.Equal(DictationAction.None, repeat.Action);
        Assert.True(repeat.Suppress);
    }

    [Fact]
    public void PushToTalk_Chord_Stops_On_Key_Release()
    {
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"),
            ActivationMode.PushToTalk);
        var matcher = new HotkeyGestureMatcher([chord]);
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;

        Assert.Equal(DictationAction.Start, matcher.Process(HotkeyKeyEvent.KeyDown("Space", 0, mods)).Action);
        matcher.SetDictationActive(true);
        Assert.Equal(DictationAction.Stop, matcher.Process(HotkeyKeyEvent.KeyUp("Space", 2000, mods)).Action);
    }

    [Fact]
    public void Chord_Requires_An_Exact_Modifier_Match()
    {
        var matcher = Default();
        var extra = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

        var result = matcher.Process(HotkeyKeyEvent.KeyDown("Space", 100, extra));
        Assert.Equal(DictationAction.None, result.Action);
        Assert.False(result.Suppress);
    }

    [Fact]
    public void AltGr_Does_Not_Fire_A_Ctrl_Alt_Chord()
    {
        // On European layouts AltGr reports as Ctrl+Alt, so typing an accented
        // character would otherwise start dictation.
        var matcher = Default();
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;

        var result = matcher.Process(
            HotkeyKeyEvent.KeyDown("Space", 100, mods, rightAltHeld: true));

        Assert.Equal(DictationAction.None, result.Action);
        Assert.False(result.Suppress);
    }

    // --- escape ---

    [Fact]
    public void Escape_Cancels_While_Dictating_And_Is_Suppressed()
    {
        var matcher = Default();
        matcher.SetDictationActive(true);

        var result = matcher.Process(HotkeyKeyEvent.KeyDown("Escape", 500));
        Assert.Equal(DictationAction.Cancel, result.Action);
        Assert.True(result.Suppress);
    }

    [Fact]
    public void A_Configured_Escape_Chord_Stops_Rather_Than_Discards()
    {
        // Binding Ctrl+Escape to toggle means "finish and type it", not "throw
        // it away" — the hardwired bare-Escape cancel must not override it.
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl, "Escape"),
            ActivationMode.Toggle);
        var matcher = new HotkeyGestureMatcher([chord]);
        matcher.SetDictationActive(true);

        var result = matcher.Process(
            HotkeyKeyEvent.KeyDown("Escape", 500, HotkeyModifiers.Ctrl));

        Assert.Equal(DictationAction.Stop, result.Action);
        Assert.True(result.Suppress);
    }

    [Fact]
    public void Bare_Escape_Still_Cancels_When_An_Escape_Chord_Is_Bound()
    {
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl, "Escape"),
            ActivationMode.Toggle);
        var matcher = new HotkeyGestureMatcher([chord]);
        matcher.SetDictationActive(true);

        var result = matcher.Process(HotkeyKeyEvent.KeyDown("Escape", 500));

        Assert.Equal(DictationAction.Cancel, result.Action);
    }

    [Fact]
    public void Escape_With_Modifiers_Held_Is_Left_To_The_OS()
    {
        // Ctrl+Esc opens the Start menu and Alt+Esc cycles windows; swallowing
        // them for the duration of every recording would be hostile.
        var matcher = Default();
        matcher.SetDictationActive(true);

        var result = matcher.Process(
            HotkeyKeyEvent.KeyDown("Escape", 500, HotkeyModifiers.Ctrl));

        Assert.Equal(DictationAction.None, result.Action);
        Assert.False(result.Suppress);
    }

    [Fact]
    public void Escape_Passes_Through_When_Not_Dictating()
    {
        var matcher = Default();

        var result = matcher.Process(HotkeyKeyEvent.KeyDown("Escape", 500));
        Assert.Equal(DictationAction.None, result.Action);
        Assert.False(result.Suppress);
    }

    // --- binding set management ---

    [Fact]
    public void Invalid_Bindings_Are_Ignored()
    {
        var invalid = new DictationHotkey(
            HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right),
            ActivationMode.Toggle); // holds cannot toggle

        var matcher = new HotkeyGestureMatcher([invalid]);
        Assert.Equal(DictationAction.None, matcher.Process(RCtrlDown(0)).Action);
    }

    [Fact]
    public void UpdateBindings_Replaces_The_Active_Set()
    {
        var matcher = Default();
        matcher.UpdateBindings([DictationHotkeyDefaults.ChordFallback]);

        // The hold binding is gone.
        Assert.Equal(DictationAction.None, matcher.Process(RCtrlDown(0)).Action);
        Assert.Null(matcher.NextTimeoutMs);

        // The chord still works.
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;
        Assert.Equal(DictationAction.Start, matcher.Process(HotkeyKeyEvent.KeyDown("Space", 100, mods)).Action);
    }

    [Fact]
    public void Empty_Binding_Set_Does_Nothing()
    {
        var matcher = new HotkeyGestureMatcher([]);

        Assert.Equal(HotkeyMatchResult.None, matcher.Process(RCtrlDown(0)));
        Assert.Equal(HotkeyMatchResult.None, matcher.Process(HotkeyKeyEvent.KeyDown("Space", 100,
            HotkeyModifiers.Ctrl | HotkeyModifiers.Alt)));
    }
}
