using Parlotype.Core.Hotkeys;
using Parlotype.Platform.Hotkeys;
using SharpHook.Data;
using Xunit;

namespace Parlotype.Tests;

public class KeyCodeMapperTests
{
    [Theory]
    [InlineData("Space", KeyCode.VcSpace)]
    [InlineData("Enter", KeyCode.VcEnter)]
    [InlineData("A", KeyCode.VcA)]
    [InlineData("Z", KeyCode.VcZ)]
    [InlineData("F1", KeyCode.VcF1)]
    [InlineData("F12", KeyCode.VcF12)]
    [InlineData("0", KeyCode.Vc0)]
    [InlineData("9", KeyCode.Vc9)]
    [InlineData("Delete", KeyCode.VcDelete)]
    public void ToKeyCode_Maps_Known_Keys(string keyName, KeyCode expected)
    {
        var result = KeyCodeMapper.ToKeyCode(keyName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToKeyCode_Returns_Null_For_Unknown()
    {
        Assert.Null(KeyCodeMapper.ToKeyCode("NonExistentKey"));
    }

    [Fact]
    public void ToKeyCode_Is_Case_Insensitive()
    {
        Assert.Equal(KeyCode.VcSpace, KeyCodeMapper.ToKeyCode("space"));
        Assert.Equal(KeyCode.VcA, KeyCodeMapper.ToKeyCode("a"));
    }

    [Theory]
    [InlineData(KeyCode.VcSpace, "Space")]
    [InlineData(KeyCode.VcA, "A")]
    [InlineData(KeyCode.VcF1, "F1")]
    public void ToKeyName_Maps_Known_Codes(KeyCode code, string expected)
    {
        var result = KeyCodeMapper.ToKeyName(code);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToKeyName_Returns_Null_For_Unmapped()
    {
        Assert.Null(KeyCodeMapper.ToKeyName(KeyCode.VcUndefined));
    }

    [Theory]
    [InlineData(KeyCode.VcLeftControl, true)]
    [InlineData(KeyCode.VcRightControl, true)]
    [InlineData(KeyCode.VcLeftAlt, true)]
    [InlineData(KeyCode.VcRightAlt, true)]
    [InlineData(KeyCode.VcLeftShift, true)]
    [InlineData(KeyCode.VcRightShift, true)]
    [InlineData(KeyCode.VcLeftMeta, true)]
    [InlineData(KeyCode.VcRightMeta, true)]
    [InlineData(KeyCode.VcSpace, false)]
    [InlineData(KeyCode.VcA, false)]
    public void IsModifierKey_Identifies_Modifiers(KeyCode code, bool expected)
    {
        Assert.Equal(expected, KeyCodeMapper.IsModifierKey(code));
    }

    [Fact]
    public void ToHotkeyModifiers_Maps_Ctrl()
    {
        var result = KeyCodeMapper.ToHotkeyModifiers(EventMask.LeftCtrl);
        Assert.True(result.HasFlag(HotkeyModifiers.Ctrl));
    }

    [Fact]
    public void ToHotkeyModifiers_Maps_Alt()
    {
        var result = KeyCodeMapper.ToHotkeyModifiers(EventMask.LeftAlt);
        Assert.True(result.HasFlag(HotkeyModifiers.Alt));
    }

    [Fact]
    public void ToHotkeyModifiers_Maps_Shift()
    {
        var result = KeyCodeMapper.ToHotkeyModifiers(EventMask.LeftShift);
        Assert.True(result.HasFlag(HotkeyModifiers.Shift));
    }

    [Fact]
    public void ToHotkeyModifiers_Maps_Combined()
    {
        var result = KeyCodeMapper.ToHotkeyModifiers(EventMask.LeftCtrl | EventMask.LeftShift);
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, result);
    }

    [Fact]
    public void ToHotkeyModifiers_None_Returns_None()
    {
        var result = KeyCodeMapper.ToHotkeyModifiers(EventMask.None);
        Assert.Equal(HotkeyModifiers.None, result);
    }

    [Fact]
    public void RoundTrip_Name_To_Code_To_Name()
    {
        foreach (var name in new[] { "Space", "A", "F1", "Enter", "Delete", "0" })
        {
            var code = KeyCodeMapper.ToKeyCode(name);
            Assert.NotNull(code);
            var back = KeyCodeMapper.ToKeyName(code!.Value);
            Assert.Equal(name, back);
        }
    }
}
