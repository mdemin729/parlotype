using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class HotkeyHintTests
{
    [Fact]
    public void Prefers_The_PushToTalk_Binding()
    {
        var hint = HotkeyHint.Describe(DictationHotkeyDefaults.All);

        Assert.Equal("Hold Right Ctrl to talk · Esc to cancel", hint);
    }

    [Fact]
    public void Falls_Back_To_The_First_Binding_When_Nothing_Is_PushToTalk()
    {
        var hint = HotkeyHint.Describe([DictationHotkeyDefaults.Toggle]);

        Assert.Equal("Double-tap Ctrl to dictate · Esc to cancel", hint);
    }

    [Fact]
    public void Names_A_PushToTalk_Chord()
    {
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"),
            ActivationMode.PushToTalk);

        Assert.Equal("Ctrl+Alt+Space to talk · Esc to cancel", HotkeyHint.Describe([chord]));
    }

    [Fact]
    public void Says_So_When_Nothing_Is_Bound()
    {
        Assert.Equal("No dictation hotkey set", HotkeyHint.Describe([]));
        Assert.Equal("No dictation hotkey set", HotkeyHint.Describe(null));
    }

    [Fact]
    public void Ignores_Invalid_Bindings()
    {
        var invalid = new DictationHotkey(
            HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right),
            ActivationMode.Toggle);

        Assert.Equal("No dictation hotkey set", HotkeyHint.Describe([invalid]));
    }

    // ---- SelectPrimary: the data half Desktop's HotkeyText.Hint builds its
    // localized sentence from (ADR-064 amendment) — same selection rules as
    // Describe above, exposed as data instead of English prose.

    [Fact]
    public void SelectPrimary_Prefers_The_PushToTalk_Binding()
    {
        var primary = HotkeyHint.SelectPrimary(DictationHotkeyDefaults.All);

        Assert.Equal(DictationHotkeyDefaults.PushToTalk, primary);
    }

    [Fact]
    public void SelectPrimary_FallsBack_ToTheFirstBinding_WhenNothingIsPushToTalk()
    {
        var primary = HotkeyHint.SelectPrimary([DictationHotkeyDefaults.Toggle]);

        Assert.Equal(DictationHotkeyDefaults.Toggle, primary);
    }

    [Fact]
    public void SelectPrimary_IsNull_WhenNothingIsBound()
    {
        Assert.Null(HotkeyHint.SelectPrimary([]));
        Assert.Null(HotkeyHint.SelectPrimary(null));
    }

    [Fact]
    public void SelectPrimary_IgnoresInvalidBindings()
    {
        var invalid = new DictationHotkey(
            HotkeyGesture.ForHold(ModifierKey.Ctrl, ModifierSide.Right),
            ActivationMode.Toggle);

        Assert.Null(HotkeyHint.SelectPrimary([invalid]));
    }
}
