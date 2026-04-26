using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Platform;
using Parlotype.Platform.TextInjection;
using Parlotype.WinUI.Services;
using Parlotype.WinUI.ViewModels;
using Parlotype.WinUI.Views;
using ZLogger;

namespace Parlotype.WinUI;

/// <summary>
/// WinUI 3 application entry point. Runs as a tray-resident app —
/// no window is shown on launch; the user interacts via the system tray icon.
/// </summary>
public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "logs");

    private TaskbarIcon? _notifyIcon;
    private TranscribeWindow? _transcribeWindow;
    private SettingsWindow? _settingsWindow;
    private IGlobalHotkeyService? _hotkeyService;
    private DispatcherQueue? _dispatcherQueue;
    private bool _isExiting;

    /// <summary>
    /// Global service provider so windows can resolve ViewModels and services.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Directory.CreateDirectory(LogDirectory);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Wire up cross-cutting ViewModel events once (VM is a singleton).
        var transcribeVm = Services.GetRequiredService<TranscribeViewModel>();
        transcribeVm.SettingsRequested += (_, _) => ShowSettingsWindow();

        var appearanceVm = Services.GetRequiredService<AppearanceViewModel>();
        appearanceVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

        InitializeTrayIcon();
        InitializeHotkeyService();
        _ = LoadAndApplyThemeAsync();
    }

    // ------------------------------------------------------------------
    //  Dependency Injection
    // ------------------------------------------------------------------

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Logging ──────────────────────────────────────────────────
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

        // ── Platform services (audio pipeline, VAD, Whisper, settings…) ──
        services.AddPlatformServices();

        // ── App-specific service overrides ────────────────────────────
        services.AddSingleton<IModelDownloadService, WinUIModelDownloadDialogService>();
        services.AddSingleton<ITargetWindowTracker, Win32TargetWindowTracker>();
        services.AddSingleton<ITextInjectionService, ClipboardTextInjectionService>();

        // ── ViewModels (singletons so state survives window close/reopen) ─
        services.AddSingleton<TranscribeViewModel>();
        services.AddSingleton<AudioSettingsViewModel>();
        services.AddSingleton<SpeechModelViewModel>();
        services.AddSingleton<HotkeySettingsViewModel>();
        services.AddSingleton<AppearanceViewModel>();
    }

    // ------------------------------------------------------------------
    //  System Tray Icon
    // ------------------------------------------------------------------

    private void InitializeTrayIcon()
    {
        var showCommand = new RelayCommand(ShowTranscribeWindow);
        var settingsCommand = new RelayCommand(ShowSettingsWindow);
        var exitCommand = new RelayCommand(ExitApplication);

        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "Parlotype",
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/parlotype.ico")),
            LeftClickCommand = showCommand,
            NoLeftClickDelay = true,
            ContextFlyout = new MenuFlyout
            {
                Items =
                {
                    new MenuFlyoutItem { Text = "Open", Command = showCommand },
                    new MenuFlyoutItem { Text = "Settings", Command = settingsCommand },
                    new MenuFlyoutSeparator(),
                    new MenuFlyoutItem { Text = "Exit", Command = exitCommand },
                }
            }
        };

        _notifyIcon.ForceCreate();
    }

    // ------------------------------------------------------------------
    //  Global Hotkey
    // ------------------------------------------------------------------

    private void InitializeHotkeyService()
    {
        _hotkeyService = Services.GetRequiredService<IGlobalHotkeyService>();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _ = _hotkeyService.StartAsync();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _dispatcherQueue?.TryEnqueue(ShowTranscribeWindow);
    }

    // ------------------------------------------------------------------
    //  Theme Management
    // ------------------------------------------------------------------

    private async Task LoadAndApplyThemeAsync()
    {
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            var theme = await settings.GetAsync<AppTheme>(SettingsKeys.SelectedTheme);
            ApplyTheme(theme);
        }
        catch
        {
            // Fall back to system default on failure.
        }
    }

    /// <summary>
    /// Applies the given <see cref="AppTheme"/> to all open windows.
    /// Call from AppearanceViewModel when the user changes the theme at runtime.
    /// </summary>
    public void ApplyTheme(AppTheme theme)
    {
        var elementTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        if (_transcribeWindow?.Content is FrameworkElement transcribeRoot)
            transcribeRoot.RequestedTheme = elementTheme;

        if (_settingsWindow?.Content is FrameworkElement settingsRoot)
            settingsRoot.RequestedTheme = elementTheme;
    }

    private async void ApplyThemeToWindow(Window window)
    {
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            var theme = await settings.GetAsync<AppTheme>(SettingsKeys.SelectedTheme);

            var elementTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            if (window.Content is FrameworkElement root)
                root.RequestedTheme = elementTheme;
        }
        catch
        {
            // Fall back to system default.
        }
    }

    // ------------------------------------------------------------------
    //  Window Management
    // ------------------------------------------------------------------

    public void ShowTranscribeWindow()
    {
        if (_transcribeWindow is null)
        {
            var viewModel = Services.GetRequiredService<TranscribeViewModel>();
            _transcribeWindow = new TranscribeWindow(viewModel);
            _transcribeWindow.Closed += OnTranscribeWindowClosed;
            ApplyThemeToWindow(_transcribeWindow);
        }

        _transcribeWindow.Activate();
    }

    private void OnTranscribeWindowClosed(object sender, WindowEventArgs args)
    {
        if (_isExiting) return;

        args.Handled = true;
        _transcribeWindow?.AppWindow.Hide();
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += OnSettingsWindowClosed;
            ApplyThemeToWindow(_settingsWindow);
        }

        _settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object sender, WindowEventArgs args)
    {
        if (_isExiting) return;

        args.Handled = true;
        _settingsWindow?.AppWindow.Hide();
    }

    // ------------------------------------------------------------------
    //  Lifecycle / Disposal
    // ------------------------------------------------------------------

    private async void ExitApplication()
    {
        _isExiting = true;

        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _ = _hotkeyService.StopAsync(); // best-effort; process is about to exit
        }

        _transcribeWindow?.Close();
        _transcribeWindow = null;

        _settingsWindow?.Close();
        _settingsWindow = null;

        _notifyIcon?.Dispose();
        _notifyIcon = null;

        if (Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (Services as IDisposable)?.Dispose();

        Exit();
    }
}
