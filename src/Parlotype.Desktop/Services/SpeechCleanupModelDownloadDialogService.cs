using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Parlotype.Platform.TextProcessing;

namespace Parlotype.Desktop.Services;

/// <summary>
/// What the Speech cleanup page needs from the model on disk: is it there, get
/// it, remove it. An interface so the page's view model can be tested without
/// opening a window.
/// </summary>
public interface ISpeechCleanupModelInstaller
{
    bool IsInstalled(SpeechCleanupModelInfo model);

    /// <summary>
    /// Ensures the model is on disk, asking the user before downloading.
    /// Returns false when they declined, cancelled, or the download failed.
    /// </summary>
    Task<bool> TryInstallAsync(SpeechCleanupModelInfo model, CancellationToken cancellationToken = default);

    Task DeleteAsync(SpeechCleanupModelInfo model);
}

/// <summary>
/// Shows the shared confirm-and-progress dialog before downloading a speech
/// cleanup model (ADR-073). Same shape as the other model download dialogs.
/// </summary>
public sealed class SpeechCleanupModelDownloadDialogService : ISpeechCleanupModelInstaller
{
    private readonly SpeechCleanupModelDownloadService _downloader;
    private readonly ILogger<SpeechCleanupModelDownloadDialogService> _logger;

    public SpeechCleanupModelDownloadDialogService(
        SpeechCleanupModelDownloadService downloader,
        ILogger<SpeechCleanupModelDownloadDialogService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    public bool IsInstalled(SpeechCleanupModelInfo model) => _downloader.IsModelCached(model);

    public Task DeleteAsync(SpeechCleanupModelInfo model) => _downloader.DeleteModelAsync(model);

    public async Task<bool> TryInstallAsync(
        SpeechCleanupModelInfo model, CancellationToken cancellationToken = default)
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

    private async Task<bool> ShowDialogAndDownloadAsync(
        SpeechCleanupModelInfo model, CancellationToken cancellationToken)
    {
        var viewModel = ModelDownloadViewModel.ForSpeechCleanupModel(model.DisplayName, model.DiskSize);

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
                viewModel.StatusText = Strings.Format_ModelDownload_DownloadingFormat(model.DisplayName);

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
                    viewModel.StatusText = Strings.Format_ModelDownload_SucceededFormat(model.DisplayName);
                    _logger.LogInformation("Speech cleanup model {ModelId} downloaded successfully", model.ModelId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Speech cleanup model download cancelled by user");
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Speech cleanup model download failed");
                    viewModel.IsDownloading = false;
                    viewModel.StatusText = Strings.Format_ModelDownload_FailedFormat(ex.Message);
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
