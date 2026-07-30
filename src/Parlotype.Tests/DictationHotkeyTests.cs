using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class DictationHotkeyTests
{
    [Theory]
    [InlineData(HotkeyGestureKind.HoldModifier, ActivationMode.PushToTalk, true)]
    [InlineData(HotkeyGestureKind.HoldModifier, ActivationMode.Toggle, false)]
    [InlineData(HotkeyGestureKind.DoubleTapModifier, ActivationMode.Toggle, true)]
    [InlineData(HotkeyGestureKind.DoubleTapModifier, ActivationMode.PushToTalk, false)]
    [InlineData(HotkeyGestureKind.Chord, ActivationMode.PushToTalk, true)]
    [InlineData(HotkeyGestureKind.Chord, ActivationMode.Toggle, true)]
    public void Mode_Matrix(HotkeyGestureKind kind, ActivationMode mode, bool allowed)
    {
        Assert.Equal(allowed, DictationHotkey.IsModeAllowed(kind, mode));
    }

    [Fact]
    public void Hold_In_Toggle_Mode_Is_Invalid()
    {
        var hotkey = new DictationHotkey(
            HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right),
            ActivationMode.Toggle);

        Assert.False(hotkey.IsValid);
    }

    [Fact]
    public void Chord_Without_A_Modifier_Is_Invalid()
    {
        var hotkey = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.None, "Space"),
            ActivationMode.Toggle);

        Assert.False(hotkey.IsValid);
    }

    [Fact]
    public void Defaults_Are_All_Valid()
    {
        Assert.All(DictationHotkeyDefaults.All, h => Assert.True(h.IsValid));
    }

    [Fact]
    public void Default_Set_Matches_The_Documented_Gestures()
    {
        Assert.Collection(DictationHotkeyDefaults.All,
            h =>
            {
                Assert.Equal("Hold Right Ctrl", h.DisplayString);
                Assert.Equal(ActivationMode.PushToTalk, h.Mode);
            },
            h =>
            {
                Assert.Equal("Double-tap Ctrl", h.DisplayString);
                Assert.Equal(ActivationMode.Toggle, h.Mode);
            },
            h =>
            {
                Assert.Equal("Ctrl+Alt+Space", h.DisplayString);
                Assert.Equal(ActivationMode.Toggle, h.Mode);
            });
    }

    [Theory]
    [InlineData(ModifierKey.Ctrl, ModifierSide.Right, "Hold Right Ctrl")]
    [InlineData(ModifierKey.Ctrl, ModifierSide.Either, "Hold Ctrl")]
    [InlineData(ModifierKey.Alt, ModifierSide.Left, "Hold Left Alt")]
    [InlineData(ModifierKey.Meta, ModifierSide.Right, "Hold Right Win")]
    public void Hold_Display_Strings(ModifierKey modifier, ModifierSide side, string expected)
    {
        Assert.Equal(expected, HotkeyGesture.ForHold(modifier, side).DisplayString);
    }

    [Fact]
    public void DoubleTap_Display_String()
    {
        Assert.Equal("Double-tap Right Shift",
            HotkeyGesture.ForDoubleTap(ModifierKey.Shift, ModifierSide.Right).DisplayString);
    }

    [Fact]
    public void Only_Chords_Offer_A_Mode_Choice()
    {
        Assert.Equal(ActivationMode.PushToTalk,
            DictationHotkey.DefaultModeFor(HotkeyGestureKind.HoldModifier));
        Assert.Equal(ActivationMode.Toggle,
            DictationHotkey.DefaultModeFor(HotkeyGestureKind.DoubleTapModifier));
        Assert.Equal(ActivationMode.Toggle,
            DictationHotkey.DefaultModeFor(HotkeyGestureKind.Chord));
    }

    [Fact]
    public void SharesModifierKeyWith_Respects_Sides()
    {
        var rightCtrl = HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right);
        var eitherCtrl = HotkeyGesture.ForDoubleTap(ModifierKey.Ctrl, ModifierSide.Either);
        var leftCtrl = HotkeyGesture.ForDoubleTap(ModifierKey.Ctrl, ModifierSide.Left);
        var rightAlt = HotkeyGesture.ForHold(ModifierKey.Alt, ModifierSide.Right);

        Assert.True(rightCtrl.SharesModifierKeyWith(eitherCtrl));
        Assert.False(rightCtrl.SharesModifierKeyWith(leftCtrl));
        Assert.False(rightCtrl.SharesModifierKeyWith(rightAlt));
    }

    [Fact]
    public void Chords_Never_Share_A_Modifier_Key()
    {
        var chord = HotkeyGesture.ForChord(new HotkeyBinding(HotkeyModifiers.Ctrl, "Space"));
        var hold = HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right);

        Assert.False(chord.SharesModifierKeyWith(hold));
        Assert.False(hold.SharesModifierKeyWith(chord));
    }
}
