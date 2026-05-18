using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.LlamaServer;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>Row view-model for a managed llama-server install in the Installed list.</summary>
public partial class LlamaServerInstallRowVm : ObservableObject
{
    public string Id { get; }
    public string? Build { get; }
    public LlamaServerBackend? Backend { get; }
    public string BackendDisplay { get; }
    public string AbsolutePath { get; }
    public bool IsValid { get; }
    public string SubtitleText { get; }

    [ObservableProperty]
    private bool _isActive;

    public LlamaServerInstallRowVm(LlamaServerInstall install, bool isActive)
    {
        Id = install.Id;
        Build = install.Build;
        Backend = install.Backend;
        BackendDisplay = LlamaServerBackendFormatter.Display(install.Backend);
        AbsolutePath = install.AbsolutePath;
        IsValid = install.IsValid;
        _isActive = isActive;
        SubtitleText = install.IsValid
            ? AbsolutePath
            : $"{AbsolutePath}  (folder missing)";
    }
}
