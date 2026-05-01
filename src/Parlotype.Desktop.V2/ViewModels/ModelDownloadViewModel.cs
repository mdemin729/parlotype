using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.V2.ViewModels;

/// <summary>ViewModel for the model download confirmation and progress dialog.</summary>
public partial class ModelDownloadViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private string _modelSize = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    /// <summary>Parameterless constructor for designer support.</summary>
    public ModelDownloadViewModel()
    {
    }

    public ModelDownloadViewModel(string modelName, string modelSize)
    {
        _modelName = modelName;
        _modelSize = modelSize;
        _statusText = $"Download \"{modelName}\" ({modelSize}) from the internet?";
    }
}
