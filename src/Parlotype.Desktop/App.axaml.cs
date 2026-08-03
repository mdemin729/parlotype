using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.TextInjection;
using Parlotype.Core.Updates;
using Parlotype.Desktop.Onboarding;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Onboarding;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform;
using Parlotype.Platform.Speech;
using Parlotype.Platform.TextInjection;
using ZLogger;

namespace Parlotype.Desktop;

public class App : Application
{
    private static readonly string LogDirectory = AppPaths.Default.LogsDirectory;

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
            desktop.Exit += async (_, _) =>
            {
                // First, and synchronously: a downloaded update is only staged, and
                // Velopack installs nothing without an explicit apply (ADR-053).
                // This hands it to Update.exe, which waits for us to exit. Runs
                // before the awaits below because a shutdown handler is not a
                // reliable place for a continuation to resume.
                _services.GetService<IUpdateService>()?.ApplyOnExit();

                // Flush the uninstall-consent write before the process goes away —
                // it gates a destructive action taken later by a process that
                // cannot ask.
                var dataSettings = _services.GetService<DataSettingsViewModel>();
                if (dataSettings is not null)
                    await dataSettings.PendingWrite;

                _hotkeyCoordinator?.Dispose();

                // Stop the llama-server sidecar (if running) before exiting
                var recognizer = _services.GetService<ISpeechRecognizer>();
                if (recognizer is not null)
                    await recognizer.DisposeAsync();
            };
        }

        _hotkeyCoordinator = _services.GetRequiredService<HotkeyCoordinator>();
        _ = _hotkeyCoordinator.StartAsync();

        // First-run onboarding tour (ADR-055). Fire-and-forget like its
        // neighbours; the service catches its own failures — a broken tour
        // must never stop the app from starting.
        _ = _services.GetRequiredService<IOnboardingService>().MaybeShowOnFirstRunAsync();

        _ = Task.Run(() => LogNvidiaEnvironmentAsync(_services));
        _ = Task.Run(() => LogVulkanEnvironmentAsync(_services));

        // Fire-and-forget: the updater schedules its own delayed first check and
        // must never hold up the first window paint (ADR-053).
        _ = Task.Run(() => StartUpdateCheckerAsync(_services));

        // Warm the speech model in the background so the user's first record press
        // is instant instead of blocking on a cold model load. Best-effort — the
        // on-button loading spinner still covers any cold start.
        _ = Task.Run(() => PrewarmSpeechModelAsync(_services));

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartUpdateCheckerAsync(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<App>>();
        try
        {
            await provider.GetRequiredService<IUpdateService>().StartAsync();
        }
        catch (Exception ex)
        {
            // A broken updater must never stop the app from transcribing.
            logger.LogWarning(ex, "Could not start the update checker");
        }
    }

    private static async Task PrewarmSpeechModelAsync(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<App>>();
        try
        {
            // Opt-in (ADR-038): skip prewarm unless the user enabled it in Settings.
            var settings = provider.GetRequiredService<ISettingsService>();
            var saved = await settings.GetAsync<string>(SettingsKeys.PrewarmModelOnStartup);
            if (!bool.TryParse(saved, out var enabled) || !enabled)
            {
                logger.LogInformation("Speech-model prewarm disabled — loading on first use");
                return;
            }

            logger.LogInformation("Speech-model prewarm enabled — warming in background");
            await provider.GetRequiredService<TranscribeViewModel>().PrewarmAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Background speech-model prewarm failed");
        }
    }

    private static async Task LogVulkanEnvironmentAsync(IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<App>>();
        try
        {
            var vulkanProvider = provider.GetRequiredService<IVulkanEnvironmentProvider>();
            var info = await vulkanProvider.GetAsync().ConfigureAwait(false);

            if (!info.HasVulkanLoader)
            {
                logger.LogInformation(
                    "No Vulkan loader detected on this system (SDK installed: {Sdk})",
                    info.SdkInstalled ? "yes" : "no");
                return;
            }

            var devices = info.Devices.Count > 0
                ? string.Join(", ", info.Devices
                    .Select(d => $"{d.Name} ({d.DeviceType}, api {d.ApiVersion}, driver {d.DriverVersion})"))
                : "(none)";

            logger.LogInformation(
                "Vulkan environment detected: Loader={Loader}; SDK={Sdk}; Devices=[{Devices}]",
                info.LoaderVersion ?? "unknown",
                info.SdkInstalled ? info.SdkPath : "(not installed)",
                devices);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to detect Vulkan environment");
        }
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
            // The rolling file persists on disk: cap it at Information so Debug
            // detail (settings values, server output, per-chunk pipeline state)
            // never lands in long-lived files. Console keeps full Debug for dev.
            // (Security audit 2026-07-11, S1.)
            logging.AddFilter<ZLogger.Providers.ZLoggerRollingFileLoggerProvider>(
                category: null, level: LogLevel.Information);
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
        // Override the Platform default with a UI-aware wrapper that shows a modal
        // progress dialog. The wrapper depends on the concrete LlamaServerInstaller
        // (registered by Platform); the last AddSingleton wins when ILlamaServerInstaller
        // is resolved, so the dialog service is what consumers see.
        services.AddSingleton<ILlamaServerInstaller, LlamaServerInstallDialogService>();

        services.AddSingleton<ITargetWindowTracker, Win32TargetWindowTracker>();
        if (Program.TextInjectionMode == TextInjectionMode.SharpHook)
            services.AddSingleton<ITextInjectionService, SharpHookTextInjectionService>();
        else
            services.AddSingleton<ITextInjectionService, ClipboardTextInjectionService>();

        services.AddSingleton<Gemma4ModelDownloadDialogService>();
        services.AddSingleton<ParakeetModelDownloadDialogService>();
        // Override Platform's headless IParakeetModelProvider with the dialog
        // wrapper (last registration wins) so the recognizer's first-use model
        // download shows progress and can be cancelled (ADR-042) — the same
        // trick as ILlamaServerInstaller above.
        services.AddSingleton<IParakeetModelProvider>(sp => sp.GetRequiredService<ParakeetModelDownloadDialogService>());

        services.AddSingleton<SpeechEngineSettingsViewModel>();
        services.AddSingleton<MicrophoneSettingsViewModel>();
        services.AddSingleton<SilenceTimeoutSettingsViewModel>();
        services.AddSingleton<WhisperModelSettingsViewModel>();
        services.AddSingleton<RuntimeSettingsViewModel>();
        services.AddSingleton<WhisperOutputSettingsViewModel>();
        // Shared source→target relationship consumed by the Language page (and the
        // Transcribe quick picker in Phase 2) so the surfaces never drift.
        services.AddSingleton<LanguageRelationshipViewModel>();
        services.AddSingleton<LanguageSelectionSettingsViewModel>();
        services.AddSingleton<Gemma4ModelSettingsViewModel>();
        services.AddSingleton<ParakeetModelSettingsViewModel>();
        services.AddSingleton<CloudProviderSettingsViewModel>();
        services.AddSingleton<PromptSettingsViewModel>();
        services.AddSingleton<LlamaCppSettingsViewModel>();
        services.AddSingleton<HotkeySettingsViewModel>();
        services.AddSingleton<ThemeSettingsViewModel>();
        services.AddSingleton<UpdateSettingsViewModel>();
        services.AddSingleton<DataSettingsViewModel>();
        services.AddSingleton<HelpSettingsViewModel>();
        services.AddSingleton<SettingsWindowViewModel>();
        services.AddSingleton<TranscribeViewModel>();
        services.AddSingleton<OnboardingWizardViewModel>();
        services.AddSingleton<AppViewModel>();

        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IUserDialogService, UserDialogService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<HotkeyCoordinator>();
        services.AddSingleton<OnboardingHighlightService>();
        services.AddSingleton<IOnboardingService, OnboardingService>();

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
