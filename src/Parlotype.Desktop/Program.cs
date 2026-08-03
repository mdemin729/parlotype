using System.Text.Json;
using Avalonia;
using Parlotype.Core.Settings;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.Services;
using Velopack;
using Velopack.Logging;

namespace Parlotype.Desktop;

public static class Program
{
    internal static TextInjectionMode TextInjectionMode { get; private set; } = TextInjectionMode.Clipboard;

    /// <summary>
    /// The lock this process holds for as long as it runs. <see cref="App"/>
    /// reads it to attach the activation listener once the window manager
    /// exists; it is created here because the check has to happen before
    /// Avalonia starts.
    /// </summary>
    internal static SingleInstanceGuard? SingleInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // MUST be the first statement (ADR-053). During install, update and
        // uninstall, Velopack re-invokes this same executable with hook arguments
        // and expects it to run the hook and exit within seconds. Anything that
        // initialises first — Avalonia, DI, ZLogger, the arg parser below — either
        // delays that past Velopack's timeout or puts a window on screen during a
        // silent install. Run() returns normally for an ordinary launch.
        var velopack = VelopackApp.Build()
            .SetLogger(VelopackFileLogger.Instance)
            .OnFirstRun(OnFirstRun);

        // The FastCallback hooks are Windows-only: on macOS and Linux uninstalling
        // is just deleting the bundle, and Velopack never gets to run anything.
        if (OperatingSystem.IsWindows())
            velopack = velopack.OnBeforeUninstallFastCallback(OnBeforeUninstall);

        velopack.Run();

        ParseArgs(args);

        // After Velopack (its install/update/uninstall hooks re-invoke this exe
        // and must never be turned away as a "second instance") and before
        // Avalonia (nothing should be on screen if we are about to exit).
        SingleInstance = SingleInstanceGuard.Acquire();
        if (!SingleInstance.IsPrimary)
        {
            var activated = SingleInstance.SignalPrimary();
            VelopackFileLogger.Instance.LogInformation(
                activated
                    ? "Parlotype is already running — asked it to show itself and exiting"
                    : "Parlotype is already running — exiting");

            SingleInstance.Dispose();
            SingleInstance = null;
            return;
        }

        using (SingleInstance)
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Runs once, on the first launch after installation. Creates the data
    /// directories up front so the first model download and the first settings
    /// write do not race to create them.
    /// </summary>
    private static void OnFirstRun(SemanticVersion version)
    {
        var log = VelopackFileLogger.Instance;
        log.LogInformation($"First run after install of v{version}");

        var paths = AppPaths.Default;
        foreach (var directory in new[] { paths.DataDirectory, paths.SettingsDirectory, paths.ModelsDirectory, paths.LogsDirectory })
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                // Non-fatal: whoever writes there first will retry and surface a
                // real error with context the user can act on.
                log.LogWarning(ex, $"Could not pre-create {directory}");
            }
        }
    }

    /// <summary>
    /// Runs immediately before Velopack removes the install. Keeps user data by
    /// default; deletes it only when the user asked for that in advance via
    /// <see cref="SettingsKeys.UninstallRemovesUserData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This hook may not show UI, so it cannot ask whether to delete several GB of
    /// downloaded models. It therefore never decides on its own — it reads a
    /// consent flag the user set in Settings → Application → Data while a real
    /// dialog was on screen, and executes that (ADR-053). Unset means keep, which
    /// is also the right default for the many uninstalls that are really
    /// troubleshooting reinstalls.
    /// </para>
    /// <para>
    /// Runs before DI and <c>ISettingsService</c> exist, so it reads settings.json
    /// directly. Everything is best-effort: a failure here must not block the
    /// uninstall, and leaving data behind is always the safe failure mode.
    /// </para>
    /// </remarks>
    private static void OnBeforeUninstall(SemanticVersion version)
    {
        var log = VelopackFileLogger.Instance;
        var paths = AppPaths.Default;

        try
        {
            var megabytes = MeasureDirectoryMegabytes(paths.ModelsDirectory);

            if (!UserAskedForDataRemoval(paths.SettingsFilePath))
            {
                log.LogInformation(
                    $"Uninstalling v{version}. Keeping user data in {paths.DataDirectory} "
                    + $"({megabytes:F0} MB of models under {paths.ModelsDirectory}). "
                    + "Delete that folder by hand to remove it.");
                return;
            }

            log.LogInformation(
                $"Uninstalling v{version}. Removing {paths.DataDirectory} "
                + $"({megabytes:F0} MB of models) — the user opted into this in Settings.");

            if (Directory.Exists(paths.DataDirectory))
                Directory.Delete(paths.DataDirectory, recursive: true);

            log.LogInformation("User data removed.");
        }
        catch (Exception ex)
        {
            // Includes the partially-deleted case (a locked file, a slow disk). The
            // app is going away regardless, so the worst outcome is leftover files
            // the user can delete by hand — never a blocked uninstall.
            log.LogWarning(ex, $"Uninstalling v{version}. User-data cleanup did not complete");
        }
    }

    /// <summary>
    /// Reads <see cref="SettingsKeys.UninstallRemovesUserData"/> straight from
    /// settings.json. Any problem — missing file, corrupt JSON, absent key —
    /// answers false, so data is only ever deleted on an unambiguous opt-in.
    /// </summary>
    private static bool UserAskedForDataRemoval(string settingsFilePath)
    {
        try
        {
            if (!File.Exists(settingsFilePath))
                return false;

            using var document = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
            if (!document.RootElement.TryGetProperty(SettingsKeys.UninstallRemovesUserData, out var value))
                return false;

            // JsonFileStore round-trips settings as a flat dictionary and the UI
            // writes bools via bool.ToString(), so the stored form is the string
            // "True". A raw JSON true is accepted as well in case the file was
            // hand-edited — anything else is not an opt-in.
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                _ => false,
            };
        }
        catch (Exception ex)
        {
            VelopackFileLogger.Instance.LogWarning(ex, "Could not read the uninstall data-removal preference");
            return false;
        }
    }

    private static double MeasureDirectoryMegabytes(string directory)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            return info.Exists
                ? info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024d / 1024d
                : 0d;
        }
        catch
        {
            return 0d;
        }
    }

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
