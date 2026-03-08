using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void Default_Binding_Is_CtrlShiftSpace()
    {
        var binding = HotkeyBinding.Default;

        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, binding.Modifiers);
        Assert.Equal("Space", binding.Key);
    }

    [Fact]
    public void DisplayString_Shows_Correct_Format()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");
        Assert.Equal("Ctrl+Shift+Space", binding.DisplayString);
    }

    [Fact]
    public void DisplayString_All_Modifiers()
    {
        var binding = new HotkeyBinding(
            HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Meta,
            "A");
        Assert.Equal("Ctrl+Alt+Shift+Win+A", binding.DisplayString);
    }

    [Fact]
    public void DisplayString_Single_Modifier()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Alt, "Space");
        Assert.Equal("Alt+Space", binding.DisplayString);
    }

    [Fact]
    public void IsValid_With_Modifier_And_Key()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl, "Space");
        Assert.True(binding.IsValid);
    }

    [Fact]
    public void IsValid_Without_Modifier_Is_False()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.None, "Space");
        Assert.False(binding.IsValid);
    }

    [Fact]
    public void IsValid_Without_Key_Is_False()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl, "");
        Assert.False(binding.IsValid);
    }

    [Fact]
    public void IsValid_Whitespace_Key_Is_False()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl, "  ");
        Assert.False(binding.IsValid);
    }

    [Fact]
    public void Record_Equality()
    {
        var a = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");
        var b = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_Inequality_Different_Key()
    {
        var a = new HotkeyBinding(HotkeyModifiers.Ctrl, "Space");
        var b = new HotkeyBinding(HotkeyModifiers.Ctrl, "A");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Record_Inequality_Different_Modifiers()
    {
        var a = new HotkeyBinding(HotkeyModifiers.Ctrl, "Space");
        var b = new HotkeyBinding(HotkeyModifiers.Alt, "Space");
        Assert.NotEqual(a, b);
    }
}
