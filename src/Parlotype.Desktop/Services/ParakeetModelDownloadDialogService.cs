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
/// Parakeet model (encoder + decoder + joiner + tokens). Mirrors
/// <see cref="Gemma4ModelDownloadDialogService"/> but targets the Parakeet catalog.
/// </summary>
public sealed class ParakeetModelDownloadDialogService
{
    private readonly ParakeetModelDownloadService _downloader;
    private readonly ILogger<ParakeetModelDownloadDialogService> _logger;

    public ParakeetModelDownloadDialogService(
        ParakeetModelDownloadService downloader,
        ILogger<ParakeetModelDownloadDialogService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the model's files are present, prompting the user to download
    /// if not. Returns true when the model is cached on completion.
    /// </summary>
    public async Task<bool> EnsureModelAsync(ParakeetModelInfo model, CancellationToken cancellationToken = default)
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

    private async Task<bool> ShowDialogAndDownloadAsync(ParakeetModelInfo model, CancellationToken cancellationToken)
    {
        var viewModel = ModelDownloadViewModel.ForParakeetModel(model.DisplayName, model.DiskSize);

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
                    _logger.LogInformation("Parakeet model {ModelId} downloaded successfully", model.ModelId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Parakeet model download cancelled by user");
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Parakeet model download failed");
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
