using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// ViewModel for the generic download confirmation and progress dialog.
/// Used by both the Whisper model downloader and the managed llama-server
/// installer; callers supply the title, item description, prompt text,
/// and button label.
/// </summary>
public partial class ModelDownloadViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Download";

    [ObservableProperty]
    private string _itemName = string.Empty;

    [ObservableProperty]
    private string _itemSize = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// Byte counter shown on its own line during download (e.g. "1113.7 / 8880.0 MiB").
    /// Kept separate from <see cref="StatusText"/> so its changing length does
    /// not reflow — and visibly flicker — the status line above it.
    /// </summary>
    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(ShowCancelButton))]
    [NotifyPropertyChangedFor(nameof(ShowCloseButton))]
    private bool _isComplete;

    [ObservableProperty]
    private string _downloadButtonText = "Download";

    /// <summary>Download is offered before it starts and after a failure (retry).</summary>
    public bool ShowDownloadButton => !IsDownloading && !IsComplete;

    /// <summary>Cancel is offered until the download has completed successfully.</summary>
    public bool ShowCancelButton => !IsComplete;

    /// <summary>Close replaces Download/Cancel once the download has completed.</summary>
    public bool ShowCloseButton => IsComplete;

    /// <summary>Parameterless constructor for designer support.</summary>
    public ModelDownloadViewModel()
    {
    }

    public ModelDownloadViewModel(
        string title,
        string itemName,
        string itemSize,
        string statusText,
        string downloadButtonText = "Download")
    {
        _title = title;
        _itemName = itemName;
        _itemSize = itemSize;
        _statusText = statusText;
        _downloadButtonText = downloadButtonText;
    }

    /// <summary>
    /// Thin adapter for the Whisper download path. Keeps the original
    /// "Model Download" header and prompt wording so the existing UX is
    /// unchanged.
    /// </summary>
    public static ModelDownloadViewModel ForWhisperModel(string modelName, string modelSize) =>
        new(
            title: "Model Download",
            itemName: modelName,
            itemSize: modelSize,
            statusText: $"Download \"{modelName}\" ({modelSize}) from the internet?");

    /// <summary>
    /// Adapter for the Gemma 4 download path. Notes the bundled vision
    /// projector (mmproj) so users understand why the download is large.
    /// </summary>
    public static ModelDownloadViewModel ForGemma4Model(string modelName, string modelSize) =>
        new(
            title: "Model Download",
            itemName: modelName,
            itemSize: modelSize,
            statusText: $"Download \"{modelName}\" ({modelSize}, includes vision projector) from the internet?");

    /// <summary>
    /// Adapter for the Parakeet download path (encoder + decoder + joiner +
    /// tokens, reported as one combined download).
    /// </summary>
    public static ModelDownloadViewModel ForParakeetModel(string modelName, string modelSize) =>
        new(
            title: "Model Download",
            itemName: modelName,
            itemSize: modelSize,
            statusText: $"Download \"{modelName}\" ({modelSize}) from the internet?");
}
