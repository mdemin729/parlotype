using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class HotkeyBindingCodecTests
{
    [Fact]
    public void Hold_RoundTrips()
    {
        var original = DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Right);

        var encoded = HotkeyBindingCodec.Encode(original);
        Assert.Equal("hold|Ctrl|Right|PushToTalk", encoded);
        Assert.Equal(original, HotkeyBindingCodec.Decode(encoded));
    }

    [Fact]
    public void DoubleTap_RoundTrips()
    {
        var original = DictationHotkey.DoubleTap(ModifierKey.Ctrl, ModifierSide.Either);

        var encoded = HotkeyBindingCodec.Encode(original);
        Assert.Equal("doubletap|Ctrl|Either|Toggle", encoded);
        Assert.Equal(original, HotkeyBindingCodec.Decode(encoded));
    }

    [Fact]
    public void Chord_RoundTrips()
    {
        var original = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"),
            ActivationMode.Toggle);

        var encoded = HotkeyBindingCodec.Encode(original);
        Assert.Equal("chord|Ctrl,Alt|Space|Toggle", encoded);
        Assert.Equal(original, HotkeyBindingCodec.Decode(encoded));
    }

    [Fact]
    public void Defaults_RoundTrip_As_A_Set()
    {
        var encoded = HotkeyBindingCodec.EncodeAll(DictationHotkeyDefaults.All);
        var decoded = HotkeyBindingCodec.DecodeAll(encoded);

        Assert.Equal(DictationHotkeyDefaults.All, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hold|Ctrl|Right")]              // too few parts
    [InlineData("hold|Ctrl|Right|Toggle|extra")] // too many parts
    [InlineData("wiggle|Ctrl|Right|PushToTalk")] // unknown kind
    [InlineData("hold|Banana|Right|PushToTalk")] // unknown modifier
    [InlineData("hold|Ctrl|Sideways|PushToTalk")]// unknown side
    [InlineData("hold|Ctrl|Right|Sideways")]     // unknown mode
    [InlineData("chord|Ctrl,Alt||Toggle")]       // empty key
    public void Malformed_Input_Decodes_To_Null(string? encoded)
    {
        Assert.Null(HotkeyBindingCodec.Decode(encoded));
    }

    [Fact]
    public void Gesture_Mode_Mismatch_Is_Rejected()
    {
        // A hold cannot toggle — an entry like this must not come back to life.
        Assert.Null(HotkeyBindingCodec.Decode("hold|Ctrl|Right|Toggle"));
        Assert.Null(HotkeyBindingCodec.Decode("doubletap|Ctrl|Either|PushToTalk"));
    }

    [Fact]
    public void DecodeAll_Skips_Bad_Entries_And_Keeps_Good_Ones()
    {
        // Forward compatibility: an entry written by a newer version is dropped
        // rather than taking the whole set down with it.
        var decoded = HotkeyBindingCodec.DecodeAll(
        [
            "hold|Ctrl|Right|PushToTalk",
            "quadrupletap|Ctrl|Either|Toggle",
            "chord|Ctrl,Alt|Space|Toggle"
        ]);

        Assert.Equal(2, decoded.Count);
        Assert.Equal(DictationHotkeyDefaults.PushToTalk, decoded[0]);
        Assert.Equal(DictationHotkeyDefaults.ChordFallback, decoded[1]);
    }

    [Fact]
    public void DecodeAll_Of_Null_Is_Empty()
    {
        Assert.Empty(HotkeyBindingCodec.DecodeAll(null));
    }

    [Fact]
    public void Decoding_Is_Case_Insensitive()
    {
        var decoded = HotkeyBindingCodec.Decode("HOLD|ctrl|RIGHT|pushtotalk");
        Assert.Equal(DictationHotkeyDefaults.PushToTalk, decoded);
    }
}
