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
}
