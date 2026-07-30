using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Covers the binding-set-aware <see cref="HotkeyConflictDetector.Check"/>;
/// the single-chord reserved-list API is covered by
/// <see cref="HotkeyConflictDetectorTests"/>.
/// </summary>
public class HotkeyConflictCheckTests
{
    private static DictationHotkey Chord(HotkeyModifiers modifiers, string key,
        ActivationMode mode = ActivationMode.Toggle) =>
        DictationHotkey.Chord(new HotkeyBinding(modifiers, key), mode);

    [Fact]
    public void Default_Set_Is_Internally_Consistent()
    {
        // Each default must be addable on top of the ones before it.
        var accepted = new List<DictationHotkey>();
        foreach (var binding in DictationHotkeyDefaults.All)
        {
            var conflict = HotkeyConflictDetector.Check(binding, accepted);
            Assert.False(conflict.IsBlocking, conflict.Description);
            accepted.Add(binding);
        }
    }

    [Fact]
    public void Hold_And_DoubleTap_On_The_Same_Key_Do_Not_Conflict()
    {
        // The shipped default pairing — the trackers tell them apart by timing.
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkeyDefaults.Toggle,
            [DictationHotkeyDefaults.PushToTalk]);

        Assert.Equal(HotkeyConflictSeverity.None, conflict.Severity);
    }

    [Fact]
    public void Duplicate_Chord_Is_Blocked()
    {
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkeyDefaults.ChordFallback,
            DictationHotkeyDefaults.All);

        Assert.True(conflict.IsBlocking);
        Assert.Contains("already bound", conflict.Description);
    }

    [Fact]
    public void Overlapping_Hold_Is_Blocked()
    {
        // "Hold Ctrl" (either side) would swallow the existing "Hold Right Ctrl".
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Either),
            [DictationHotkeyDefaults.PushToTalk]);

        Assert.True(conflict.IsBlocking);
    }

    [Fact]
    public void Hold_On_The_Other_Side_Is_Allowed()
    {
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Left),
            [DictationHotkeyDefaults.PushToTalk]);

        Assert.Equal(HotkeyConflictSeverity.None, conflict.Severity);
    }

    [Fact]
    public void Reserved_OS_Shortcut_Is_Blocked()
    {
        var conflict = HotkeyConflictDetector.Check(Chord(HotkeyModifiers.Meta, "L"));

        Assert.True(conflict.IsBlocking);
        Assert.Contains("Lock workstation", conflict.Description);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Meta, "H", "Voice Typing")]
    [InlineData(HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "S", "Speech Recognition")]
    [InlineData(HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "Space", "input source")]
    public void Newly_Reserved_Shortcuts_Are_Blocked(HotkeyModifiers modifiers, string key, string expected)
    {
        var conflict = HotkeyConflictDetector.Check(Chord(modifiers, key));

        Assert.True(conflict.IsBlocking);
        Assert.Contains(expected, conflict.Description);
    }

    [Fact]
    public void CtrlShiftSpace_Warns_But_Is_Allowed()
    {
        var conflict = HotkeyConflictDetector.Check(
            Chord(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space"));

        Assert.Equal(HotkeyConflictSeverity.Warning, conflict.Severity);
        Assert.False(conflict.IsBlocking);
        Assert.Contains("Visual Studio", conflict.Description);
    }

    [Fact]
    public void CtrlAlt_Letter_Warns_About_AltGr()
    {
        var conflict = HotkeyConflictDetector.Check(
            Chord(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "P"));

        Assert.Equal(HotkeyConflictSeverity.Warning, conflict.Severity);
        Assert.Contains("AltGr", conflict.Description);
    }

    [Fact]
    public void CtrlAlt_Space_Does_Not_Warn()
    {
        // Space produces no character, so AltGr cannot collide with it.
        var conflict = HotkeyConflictDetector.Check(
            Chord(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"));

        Assert.Equal(HotkeyConflictSeverity.None, conflict.Severity);
    }

    [Fact]
    public void Invalid_Binding_Is_Blocked()
    {
        var invalid = new DictationHotkey(
            HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right),
            ActivationMode.Toggle);

        Assert.True(HotkeyConflictDetector.Check(invalid).IsBlocking);
    }

    [Fact]
    public void Bare_Modifier_Gestures_Skip_The_Reserved_Chord_List()
    {
        // Win+H is reserved, but "hold the Windows key" is a different gesture.
        var conflict = HotkeyConflictDetector.Check(
            DictationHotkey.Hold(ModifierKey.Meta, ModifierSide.Right));

        Assert.Equal(HotkeyConflictSeverity.None, conflict.Severity);
    }
}
