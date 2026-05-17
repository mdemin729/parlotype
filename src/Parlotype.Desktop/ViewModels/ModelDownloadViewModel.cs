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

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _downloadButtonText = "Download";

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
}
