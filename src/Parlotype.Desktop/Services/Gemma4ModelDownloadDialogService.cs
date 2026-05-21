using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Shows a modal confirmation dialog with a progress bar before downloading a
/// Gemma 4 model variant (GGUF + mmproj). Mirrors
/// <see cref="ModelDownloadDialogService"/> but targets the Gemma 4 catalog.
/// </summary>
public sealed class Gemma4ModelDownloadDialogService
{
    private readonly Gemma4ModelDownloadService _downloader;
    private readonly ILogger<Gemma4ModelDownloadDialogService> _logger;

    public Gemma4ModelDownloadDialogService(
        Gemma4ModelDownloadService downloader,
        ILogger<Gemma4ModelDownloadDialogService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the variant's files are present, prompting the user to download
    /// if not. Returns true when the model is cached on completion.
    /// </summary>
    public async Task<bool> EnsureModelAsync(Gemma4ModelInfo model, CancellationToken cancellationToken = default)
    {
        if (_downloader.IsModelCached(model))
            return true;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(
                () => ShowDialogAndDownloadAsync(model, cancellationToken));
        }

        return await ShowDialogAndDownloadAsync(model, cancellationToken);
    }

    private async Task<bool> ShowDialogAndDownloadAsync(Gemma4ModelInfo model, CancellationToken cancellationToken)
    {
        var viewModel = ModelDownloadViewModel.ForGemma4Model(model.DisplayName, model.DiskSize);

        var dialog = new ModelDownloadDialog { DataContext = viewModel };
        var owner = GetOwnerWindow();

        var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var dialogClosed = new TaskCompletionSource<bool>();

        dialog.Closed += (_, _) =>
        {
            if (!dialog.UserConfirmed || viewModel.IsDownloading)
                downloadCts.Cancel();
            dialogClosed.TrySetResult(dialog.UserConfirmed);
        };

        var downloadButton = dialog.FindControl<Button>("DownloadButton");
        if (downloadButton is not null)
        {
            downloadButton.Click += async (_, _) =>
            {
                viewModel.IsDownloading = true;
                viewModel.StatusText = $"Downloading \"{model.DisplayName}\"...";

                var progress = new Progress<ModelDownloadProgress>(p =>
                {
                    var fraction = p.ProgressFraction;
                    if (fraction.HasValue)
                    {
                        viewModel.ProgressValue = fraction.Value * 100;
                        var receivedMb = p.BytesReceived / (1024.0 * 1024.0);
                        var totalMb = p.TotalBytes.HasValue ? p.TotalBytes.Value / (1024.0 * 1024.0) : 0;
                        viewModel.ProgressText = $"{receivedMb:F1} / {totalMb:F1} MiB";
                    }
                });

                try
                {
                    await _downloader.DownloadModelAsync(model, progress, downloadCts.Token);
                    viewModel.IsDownloading = false;
                    viewModel.IsComplete = true;
                    viewModel.StatusText = $"\"{model.DisplayName}\" downloaded successfully.";
                    _logger.LogInformation("Gemma 4 model {Variant} downloaded successfully", model.Variant);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Gemma 4 model download cancelled by user");
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gemma 4 model download failed");
                    viewModel.IsDownloading = false;
                    viewModel.StatusText = $"Download failed: {ex.Message}";
                }
            };
        }

        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            await dialog.ShowDialog(dialog);

        await dialogClosed.Task;
        downloadCts.Dispose();

        return _downloader.IsModelCached(model);
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
