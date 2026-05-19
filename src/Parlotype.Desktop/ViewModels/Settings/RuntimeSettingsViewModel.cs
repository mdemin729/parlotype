using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class RuntimeSettingsViewModel : SettingsSectionViewModelBase
{
    private const string CudaDownloadUrl = "https://developer.nvidia.com/cuda-downloads";
    private const string VulkanSdkUrl = "https://vulkan.lunarg.com/sdk/home";

    private readonly ISettingsService _settings;
    private readonly INvidiaEnvironmentProvider _nvidia;
    private readonly IVulkanEnvironmentProvider _vulkan;
    private readonly ILogger<RuntimeSettingsViewModel> _logger;

    public override string Title => "Whisper runtime";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Whisper;

    public RuntimeDisplayItem[] RuntimeOptions { get; }

    [ObservableProperty]
    private RuntimePreference _selectedRuntime = RuntimePreference.Auto;

    [ObservableProperty]
    private bool _vulkanLoaderMissing;

    /// <summary>True when no NVIDIA driver is detected at all.</summary>
    [ObservableProperty]
    private bool _cudaDriverMissing;

    /// <summary>True when an NVIDIA driver exists but no CUDA runtime library can be loaded.</summary>
    [ObservableProperty]
    private bool _cudaSdkMissing;

    /// <summary>The detected NVIDIA driver version, or null if no driver was found.</summary>
    [ObservableProperty]
    private string? _cudaDriverVersion;

    public RuntimeSettingsViewModel(
        ISettingsService settings,
        INvidiaEnvironmentProvider nvidia,
        IVulkanEnvironmentProvider vulkan,
        ILogger<RuntimeSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _nvidia = nvidia;
        _vulkan = vulkan;
        _logger = logger ?? NullLogger<RuntimeSettingsViewModel>.Instance;

        RuntimeOptions =
        [
            new(RuntimePreference.Auto, "Auto",
                "Try CUDA, then Vulkan, then CPU. Recommended.",
                SelectRuntimeCommand),
            new(RuntimePreference.Cuda, "CUDA",
                "NVIDIA GPU only. Fastest on supported NVIDIA hardware. Will not start without an NVIDIA driver.",
                SelectRuntimeCommand),
            new(RuntimePreference.Vulkan, "Vulkan",
                "Any GPU via Vulkan (AMD, Intel, NVIDIA). Requires GPU drivers with Vulkan support.",
                SelectRuntimeCommand),
            new(RuntimePreference.Cpu, "CPU",
                "Force CPU-only inference. Always works but slower for larger models.",
                SelectRuntimeCommand),
        ];

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.RuntimePreference);
        var runtime = Enum.TryParse<RuntimePreference>(saved, ignoreCase: true, out var parsed)
            ? parsed
            : RuntimePreference.Auto;
        Apply(runtime);
        await RefreshAvailabilityAsync();
    }

    private async Task RefreshAvailabilityAsync()
    {
        var nvidia = await _nvidia.GetAsync();
        var vulkan = await _vulkan.GetAsync();
        VulkanLoaderMissing = !vulkan.HasVulkanLoader;

        // Distinguish CUDA readiness states
        CudaDriverMissing = !nvidia.HasNvidia;
        CudaSdkMissing = nvidia.HasNvidia && nvidia.LoadableRuntimes.Count == 0;
        CudaDriverVersion = nvidia.DriverVersion;

        foreach (var item in RuntimeOptions)
        {
            switch (item.Type)
            {
                case RuntimePreference.Cuda:
                    item.IsAvailable = nvidia.HasNvidia && nvidia.LoadableRuntimes.Count > 0;
                    item.UnavailableReason = item.IsAvailable
                        ? null
                        : CudaDriverMissing
                            ? "No NVIDIA GPU detected — see guidance below."
                            : "CUDA toolkit not installed — see guidance below.";
                    break;
                case RuntimePreference.Vulkan:
                    item.IsAvailable = vulkan.HasVulkanLoader;
                    item.UnavailableReason = vulkan.HasVulkanLoader
                        ? null
                        : "Vulkan loader (vulkan-1.dll) not detected. Install GPU drivers or the Vulkan SDK.";
                    break;
                default:
                    item.IsAvailable = true;
                    item.UnavailableReason = null;
                    break;
            }
        }
    }

    [RelayCommand]
    private void SelectRuntime(RuntimePreference type)
    {
        if (type == SelectedRuntime)
            return;

        _logger.LogInformation("Runtime preference selected: {Type}", type);
        Apply(type);
        _ = _settings.SetAsync(SettingsKeys.RuntimePreference, type.ToString());
    }

    private void Apply(RuntimePreference type)
    {
        SelectedRuntime = type;
        foreach (var item in RuntimeOptions)
            item.IsSelected = item.Type == type;
    }

    [RelayCommand]
    private void OpenCudaDownloadLink()
    {
        _logger.LogInformation("Opening CUDA download page: {Url}", CudaDownloadUrl);
        Process.Start(new ProcessStartInfo(CudaDownloadUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenVulkanSdkLink()
    {
        _logger.LogInformation("Opening Vulkan SDK page: {Url}", VulkanSdkUrl);
        Process.Start(new ProcessStartInfo(VulkanSdkUrl) { UseShellExecute = true });
    }
}
