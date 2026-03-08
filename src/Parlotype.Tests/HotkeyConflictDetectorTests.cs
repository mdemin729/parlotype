using Parlotype.Core.Hotkeys;
using Xunit;

namespace Parlotype.Tests;

public class HotkeyConflictDetectorTests
{
    [Fact]
    public void WinL_Is_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "L");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void WinE_Is_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "E");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void WinR_Is_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "R");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void CtrlAltDelete_Is_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Delete");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void WinSpace_Is_Reserved_For_macOS_Spotlight()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "Space");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void CtrlShiftSpace_Is_Not_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");
        Assert.False(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void AltSpace_Is_Not_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Alt, "Space");
        Assert.False(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void CtrlShiftA_Is_Not_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "A");
        Assert.False(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void GetConflictDescription_Returns_Null_For_Safe_Binding()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");
        Assert.Null(HotkeyConflictDetector.GetConflictDescription(binding));
    }

    [Fact]
    public void GetConflictDescription_Returns_Description_For_Reserved()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "L");
        var desc = HotkeyConflictDetector.GetConflictDescription(binding);

        Assert.NotNull(desc);
        Assert.Contains("Lock workstation", desc);
        Assert.Contains("Win+L", desc);
    }

    [Fact]
    public void Case_Insensitive_Key_Matching()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Meta, "l");
        Assert.True(HotkeyConflictDetector.IsReserved(binding));
    }

    [Fact]
    public void Extra_Modifiers_Do_Not_Match_Reserved()
    {
        // Win+Ctrl+L is NOT Win+L
        var binding = new HotkeyBinding(HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "L");
        Assert.False(HotkeyConflictDetector.IsReserved(binding));
    }
}
