using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class ApiKeyBoxTests
{
    private static (ApiKeyBox Box, Window Host) CreateShown()
    {
        var box = new ApiKeyBox();
        var host = new Window { Content = box };
        host.Show();
        return (box, host);
    }

    [AvaloniaFact]
    public void Text_FlowsIntoInnerTextBox_AndBack()
    {
        var (box, host) = CreateShown();
        var entry = box.FindControl<TextBox>("Entry");
        Assert.NotNull(entry);

        box.Text = "sk-outer";
        Assert.Equal("sk-outer", entry!.Text);

        entry.Text = "sk-inner";
        Assert.Equal("sk-inner", box.Text);

        host.Close();
    }

    [AvaloniaFact]
    public void RevealToggle_Checked_FlipsIsRevealed_AndInnerRevealPassword()
    {
        // The click→IsChecked flip is ToggleButton's own behaviour; what's ours
        // is the binding chain toggle → ApiKeyBox.IsRevealed → TextBox.RevealPassword.
        var (box, host) = CreateShown();
        var entry = box.FindControl<TextBox>("Entry");
        var reveal = box.FindControl<ToggleButton>("Reveal");
        Assert.NotNull(entry);
        Assert.NotNull(reveal);

        Assert.False(box.IsRevealed);
        Assert.False(entry!.RevealPassword);

        reveal!.IsChecked = true;

        Assert.True(box.IsRevealed);
        Assert.True(entry.RevealPassword);

        reveal.IsChecked = false;

        Assert.False(box.IsRevealed);
        Assert.False(entry.RevealPassword);

        host.Close();
    }

    [AvaloniaFact]
    public void IsRevealed_SetFromOutside_ChecksToggleButton()
    {
        var (box, host) = CreateShown();
        var reveal = box.FindControl<ToggleButton>("Reveal");
        Assert.NotNull(reveal);

        box.IsRevealed = true;
        Assert.True(reveal!.IsChecked);

        box.IsRevealed = false;
        Assert.False(reveal.IsChecked);

        host.Close();
    }

    [AvaloniaFact]
    public void PlaceholderText_FlowsIntoInnerTextBox()
    {
        var (box, host) = CreateShown();
        var entry = box.FindControl<TextBox>("Entry");
        Assert.NotNull(entry);

        box.PlaceholderText = "sk-…";
        Assert.Equal("sk-…", entry!.PlaceholderText);

        host.Close();
    }
}
