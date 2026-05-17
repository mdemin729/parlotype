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
/// Desktop V2 implementation of <see cref="IModelDownloadService"/> that shows a modal
/// confirmation dialog with a progress bar before downloading.
/// </summary>
public sealed class ModelDownloadDialogService : IModelDownloadService
{
    private readonly HttpModelDownloadService _httpDownloader;
    private readonly ILogger<ModelDownloadDialogService> _logger;

    public ModelDownloadDialogService(HttpModelDownloadService httpDownloader, ILogger<ModelDownloadDialogService> logger)
    {
        _httpDownloader = httpDownloader;
        _logger = logger;
    }

    public bool IsModelCached(WhisperModelType modelType) =>
        _httpDownloader.IsModelCached(modelType);

    public async Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
    {
        var modelPath = _httpDownloader.GetModelPath(modelType);

        if (_httpDownloader.IsModelCached(modelType))
            return modelPath;

        // Must show dialog on UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(
                () => ShowDialogAndDownloadAsync(modelType, cancellationToken));
        }

        return await ShowDialogAndDownloadAsync(modelType, cancellationToken);
    }

    private async Task<string> ShowDialogAndDownloadAsync(WhisperModelType modelType, CancellationToken cancellationToken)
    {
        var modelInfo = WhisperModelInfo.Get(modelType);
        var viewModel = ModelDownloadViewModel.ForWhisperModel(modelInfo.DisplayName, modelInfo.DiskSize);

        var dialog = new ModelDownloadDialog { DataContext = viewModel };
        var owner = GetOwnerWindow();

        var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var dialogClosed = new TaskCompletionSource<bool>();

        dialog.Closed += (_, _) =>
        {
            if (!dialog.UserConfirmed || viewModel.IsDownloading)
            {
                downloadCts.Cancel();
            }
            dialogClosed.TrySetResult(dialog.UserConfirmed);
        };

        var downloadButton = dialog.FindControl<Button>("DownloadButton");
        if (downloadButton is not null)
        {
            downloadButton.Click += async (_, _) =>
            {
                viewModel.IsDownloading = true;
                viewModel.StatusText = $"Downloading \"{modelInfo.DisplayName}\"...";

                var progress = new Progress<ModelDownloadProgress>(p =>
                {
                    var fraction = p.ProgressFraction;
                    if (fraction.HasValue)
                    {
                        viewModel.ProgressValue = fraction.Value * 100;
                        var receivedMb = p.BytesReceived / (1024.0 * 1024.0);
                        var totalMb = p.TotalBytes.HasValue ? p.TotalBytes.Value / (1024.0 * 1024.0) : 0;
                        viewModel.StatusText = $"Downloading \"{modelInfo.DisplayName}\"... {receivedMb:F1} / {totalMb:F1} MiB";
                    }
                });

                try
                {
                    await _httpDownloader.DownloadModelAsync(modelType, progress, downloadCts.Token);
                    viewModel.StatusText = "Download complete!";
                    _logger.LogInformation("Model {Model} downloaded successfully", modelType);
                    dialog.Close();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Model download cancelled by user");
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Model download failed");
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

        if (!_httpDownloader.IsModelCached(modelType))
            throw new OperationCanceledException("Model download was cancelled.");

        return _httpDownloader.GetModelPath(modelType);
    }

    private static Window? GetOwnerWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        // Prefer an active/visible window; fall back to MainWindow
        foreach (var window in desktop.Windows)
        {
            if (window.IsVisible)
                return window;
        }

        return desktop.MainWindow;
    }
}
