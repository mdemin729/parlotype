using Avalonia.Controls.ApplicationLifetimes;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The gate that keeps the XAML previewer from starting a real Parlotype
/// (ADR-063): global keyboard hook, microphone, prewarmed speech model, all
/// outside the single-instance guard and with nothing to shut them down.
/// </summary>
public class AppRuntimeLifetimeGuardTests
{
    [Fact]
    public void DesktopLifetime_OutsideDesignMode_Bootstraps()
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime();

        Assert.Same(lifetime, App.ResolveRuntimeLifetime(lifetime, isDesignMode: false));
    }

    [Fact]
    public void DesignMode_NeverBootstraps_EvenWithADesktopLifetime()
    {
        // Belt to the lifetime check's braces: Avalonia sets Design.IsDesignMode
        // before it builds the app, so this alone stops the previewer even if a
        // future previewer starts supplying a desktop lifetime.
        Assert.Null(App.ResolveRuntimeLifetime(
            new ClassicDesktopStyleApplicationLifetime(), isDesignMode: true));
    }

    [Fact]
    public void NoLifetime_DoesNotBootstrap()
    {
        // What the previewer actually gives us today: SetupWithoutStarting()
        // leaves ApplicationLifetime null.
        Assert.Null(App.ResolveRuntimeLifetime(lifetime: null, isDesignMode: false));
    }
}
