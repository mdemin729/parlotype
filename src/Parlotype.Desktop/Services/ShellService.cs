using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Parlotype.Desktop.Services;

/// <summary>
/// <see cref="IShellService"/> backed by Avalonia's clipboard and the OS shell.
/// Owner-window resolution follows the same pattern as <see cref="UserDialogService"/>.
/// </summary>
public sealed class ShellService : IShellService
{
    private readonly ILogger<ShellService> _logger;

    public ShellService(ILogger<ShellService>? logger = null)
    {
        _logger = logger ?? NullLogger<ShellService>.Instance;
    }

    public async Task<bool> CopyTextAsync(string text)
    {
        // The clipboard hangs off a TopLevel and must be touched on the UI thread.
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => CopyTextCoreAsync(text));

        return await CopyTextCoreAsync(text);
    }

    private async Task<bool> CopyTextCoreAsync(string text)
    {
        try
        {
            // Avalonia 12 moved SetTextAsync off IClipboard onto ClipboardExtensions.
            var clipboard = GetOwnerWindow()?.Clipboard;
            if (clipboard is null)
            {
                _logger.LogWarning("No clipboard available — is a window open?");
                return false;
            }

            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy text to the clipboard");
            return false;
        }
    }

    public Task<bool> OpenDirectoryAsync(string path)
    {
        try
        {
            // The data directory is created lazily on first write, so it may not
            // exist yet on a fresh install. Opening an empty folder is a better
            // answer than an error the user can do nothing about.
            Directory.CreateDirectory(path);

            // UseShellExecute routes through the platform handler: Explorer on
            // Windows, `open` on macOS, `xdg-open` on Linux. Same approach as the
            // Vulkan SDK link in RuntimeSettingsViewModel.
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

            _logger.LogInformation("Opened {Path} in the file manager", path);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open {Path} in the file manager", path);
            return Task.FromResult(false);
        }
    }

    private static Window? GetOwnerWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        foreach (var window in desktop.Windows)
        {
            if (window.IsVisible)
                return window;
        }

        return desktop.MainWindow;
    }
}
