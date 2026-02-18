using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Parlotype.Desktop.Tests.TestAppBuilder))]

namespace Parlotype.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
                     .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
