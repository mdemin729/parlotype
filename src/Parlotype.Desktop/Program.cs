using Avalonia;
using Parlotype.Core.TextInjection;

namespace Parlotype.Desktop;

public static class Program
{
    internal static TextInjectionMode TextInjectionMode { get; private set; } = TextInjectionMode.Clipboard;

    [STAThread]
    public static void Main(string[] args)
    {
        ParseArgs(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith("--text-injection-mode=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg["--text-injection-mode=".Length..];
                TextInjectionMode = value.ToLowerInvariant() switch
                {
                    "sharp-hook" => TextInjectionMode.SharpHook,
                    "clipboard" => TextInjectionMode.Clipboard,
                    _ => TextInjectionMode.Clipboard,
                };
            }
        }
    }
}
