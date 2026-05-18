using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Parlotype.Core.LlamaServer;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views;
using Parlotype.Platform.LlamaServer;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Desktop wrapper around <see cref="LlamaServerInstaller"/> that surfaces
/// progress through the generalized <see cref="ModelDownloadDialog"/>. Uninstall
/// does not need a dialog and delegates straight to the platform installer.
/// </summary>
public sealed class LlamaServerInstallDialogService : ILlamaServerInstaller
{
    private readonly LlamaServerInstaller _inner;
    private readonly ILogger<LlamaServerInstallDialogService> _logger;

    public LlamaServerInstallDialogService(
        LlamaServerInstaller inner,
        ILogger<LlamaServerInstallDialogService> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<LlamaServerInstall> InstallAsync(
        LlamaServerVariant variant,
        IProgress<LlamaServerInstallProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(
                () => InstallOnUiThreadAsync(variant, progress, cancellationToken));
        }

        return await InstallOnUiThreadAsync(variant, progress, cancellationToken);
    }

    public Task UninstallAsync(string installId, CancellationToken cancellationToken = default)
        => _inner.UninstallAsync(installId, cancellationToken);

    private async Task<LlamaServerInstall> InstallOnUiThreadAsync(
        LlamaServerVariant variant,
        IProgress<LlamaServerInstallProgress>? outerProgress,
        CancellationToken cancellationToken)
    {
        var size = LlamaServerBackendFormatter.FormatBytes(
            variant.Bytes + (variant.CompanionBytes ?? 0));
        var backendDisplay = LlamaServerBackendFormatter.Display(variant.Backend);
        var label = $"{variant.Build} · {backendDisplay}";

        var viewModel = new ModelDownloadViewModel(
            title: "Install llama-server",
            itemName: label,
            itemSize: size,
            statusText: $"Install \"{label}\" ({size}) from GitHub?",
            downloadButtonText: "Install");

        var dialog = new ModelDownloadDialog { DataContext = viewModel };
        var owner = GetOwnerWindow();
        var installCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var dialogClosed = new TaskCompletionSource<bool>();
        LlamaServerInstall? result = null;
        Exception? installError = null;

        dialog.Closed += (_, _) =>
        {
            if (!dialog.UserConfirmed || viewModel.IsDownloading)
                installCts.Cancel();
            dialogClosed.TrySetResult(dialog.UserConfirmed);
        };

        var installButton = dialog.FindControl<Button>("DownloadButton");
        if (installButton is not null)
        {
            installButton.Click += async (_, _) =>
            {
                viewModel.IsDownloading = true;
                viewModel.StatusText = $"Installing \"{label}\"...";

                var dialogProgress = new Progress<LlamaServerInstallProgress>(p =>
                {
                    outerProgress?.Report(p);
                    UpdateDialogProgress(viewModel, label, p);
                });

                try
                {
                    result = await _inner.InstallAsync(variant, dialogProgress, installCts.Token);
                    viewModel.StatusText = "Install complete!";
                    _logger.LogInformation("Installed llama-server {Id}", result.Id);
                    dialog.Close();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("llama-server install cancelled by user");
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    installError = ex;
                    _logger.LogError(ex, "llama-server install failed");
                    viewModel.IsDownloading = false;
                    viewModel.StatusText = $"Install failed: {ex.Message}";
                }
            };
        }

        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            await dialog.ShowDialog(dialog);

        await dialogClosed.Task;
        installCts.Dispose();

        if (installError is not null)
            throw installError;
        if (result is null)
            throw new OperationCanceledException("llama-server install was cancelled.");

        return result;
    }

    private static void UpdateDialogProgress(
        ModelDownloadViewModel viewModel, string label, LlamaServerInstallProgress p)
    {
        var fraction = p.Fraction;
        if (fraction.HasValue)
            viewModel.ProgressValue = fraction.Value * 100;

        viewModel.StatusText = p.Phase switch
        {
            "downloading" => FormatDownloadStatus(label, "Downloading", p),
            "downloading-companion" => FormatDownloadStatus(label, "Downloading CUDA runtime", p),
            "verifying" => $"Verifying \"{label}\"...",
            "extracting" => $"Extracting \"{label}\"...",
            "finalizing" => $"Finalizing \"{label}\"...",
            _ => $"Installing \"{label}\"...",
        };
    }

    private static string FormatDownloadStatus(
        string label, string verb, LlamaServerInstallProgress p)
    {
        if (p.TotalBytes is > 0)
        {
            var received = p.BytesReceived / (1024.0 * 1024.0);
            var total = p.TotalBytes.Value / (1024.0 * 1024.0);
            return $"{verb} \"{label}\"... {received:F1} / {total:F1} MiB";
        }
        var receivedOnly = p.BytesReceived / (1024.0 * 1024.0);
        return $"{verb} \"{label}\"... {receivedOnly:F1} MiB";
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
