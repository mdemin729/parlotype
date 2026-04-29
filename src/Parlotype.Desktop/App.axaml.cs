using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Hotkeys;
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
            var mainVm = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            _ = mainVm.InitializeHotkeyServiceAsync();
        }

        _ = Task.Run(() => LogNvidiaEnvironmentAsync(provider));

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task LogNvidiaEnvironmentAsync(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<App>>();
        try
        {
            var nvidiaProvider = provider.GetRequiredService<INvidiaEnvironmentProvider>();
            var info = await nvidiaProvider.GetAsync().ConfigureAwait(false);

            if (!info.HasNvidia)
            {
                logger.LogInformation("No NVIDIA driver detected on this system");
                return;
            }

            var toolkits = info.InstalledToolkitVersions.Count > 0
                ? string.Join(", ", info.InstalledToolkitVersions)
                : "(none)";

            var runtimes = info.LoadableRuntimes.Count > 0
                ? string.Join(", ", info.LoadableRuntimes
                    .Select(r => $"{r.LibraryName} (runtime {r.RuntimeVersion}, driver {r.DriverVersion})"))
                : "(none)";

            logger.LogInformation(
                "NVIDIA environment detected: Driver={Driver} (max CUDA: {MaxCuda}); Installed Toolkits=[{Toolkits}]; Loadable Runtimes=[{Runtimes}]",
                info.DriverVersion ?? "unknown",
                info.DriverMaxCudaVersion ?? "unknown",
                toolkits,
                runtimes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to detect NVIDIA environment");
        }
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
