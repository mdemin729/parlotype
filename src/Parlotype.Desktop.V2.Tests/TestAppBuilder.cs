using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Parlotype.Desktop.V2.Tests.TestAppBuilder))]

namespace Parlotype.Desktop.V2.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
                     .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
