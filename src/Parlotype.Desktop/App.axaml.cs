using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Parlotype.Platform;
using Parlotype.Platform.TextInjection;
using ZLogger;

namespace Parlotype.Desktop;

public class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "logs");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);

            logging.AddZLoggerConsole(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] ",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));
                    formatter.SetSuffixFormatter($" ({0})",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Category));
                });
            });

            logging.AddZLoggerRollingFile(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] ",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));
                    formatter.SetSuffixFormatter($" ({0})",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Category));
                });
                options.FilePathSelector = (dt, seq) =>
                    Path.Combine(LogDirectory, $"parlotype-{dt:yyyy-MM-dd}_{seq:000}.log");
                options.RollingInterval = ZLogger.Providers.RollingInterval.Day;
                options.RollingSizeKB = 10_240;
            });
        });

        services.AddPlatformServices();
        services.AddSingleton<IModelDownloadService, ModelDownloadDialogService>();

        services.AddSingleton<ITargetWindowTracker, Win32TargetWindowTracker>();
        if (Program.TextInjectionMode == TextInjectionMode.SharpHook)
            services.AddSingleton<ITextInjectionService, SharpHookTextInjectionService>();
        else
            services.AddSingleton<ITextInjectionService, ClipboardTextInjectionService>();

        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        var settingsVm = provider.GetRequiredService<SettingsViewModel>();
        ApplyTheme(settingsVm.SelectedTheme);
        settingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
