using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.V2.Services;
using Parlotype.Desktop.V2.ViewModels;
using Parlotype.Desktop.V2.ViewModels.Settings;
using Parlotype.Platform;
using Parlotype.Platform.Speech;
using Parlotype.Platform.TextInjection;
using ZLogger;

namespace Parlotype.Desktop.V2;

public class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "logs");

    private IServiceProvider? _services;
    private HotkeyCoordinator? _hotkeyCoordinator;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServiceProvider();

        DataContext = _services.GetRequiredService<AppViewModel>();

        var themeVm = _services.GetRequiredService<ThemeSettingsViewModel>();
        ApplyTheme(themeVm.SelectedTheme);
        themeVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _hotkeyCoordinator?.Dispose();
        }

        _hotkeyCoordinator = _services.GetRequiredService<HotkeyCoordinator>();
        _ = _hotkeyCoordinator.StartAsync();

        _ = Task.Run(() => LogNvidiaEnvironmentAsync(_services));

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

    private static IServiceProvider BuildServiceProvider()
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
                    Path.Combine(LogDirectory, $"parlotype-v2-{dt:yyyy-MM-dd}_{seq:000}.log");
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

        services.AddSingleton<MicrophoneSettingsViewModel>();
        services.AddSingleton<WhisperModelSettingsViewModel>();
        services.AddSingleton<HotkeySettingsViewModel>();
        services.AddSingleton<SpeechSettingsViewModel>();
        services.AddSingleton<ThemeSettingsViewModel>();
        services.AddSingleton<SettingsWindowViewModel>();
        services.AddSingleton<TranscribeViewModel>();
        services.AddSingleton<AppViewModel>();

        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<HotkeyCoordinator>();

        return services.BuildServiceProvider();
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
