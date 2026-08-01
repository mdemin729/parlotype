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
    private const string VulkanSdkUrl = "https://vulkan.lunarg.com/sdk/home";

    private readonly ISettingsService _settings;
    private readonly IVulkanEnvironmentProvider _vulkan;
    private readonly IWhisperRuntimeStatus? _runtimeStatus;
    private readonly ILogger<RuntimeSettingsViewModel> _logger;

    public override string Title => "Whisper runtime";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Whisper;

    public RuntimeDisplayItem[] RuntimeOptions { get; }

    [ObservableProperty]
    private RuntimePreference _selectedRuntime = RuntimePreference.Auto;

    [ObservableProperty]
    private bool _vulkanLoaderMissing;

    /// <summary>
    /// True when the selected runtime differs from the one this process already
    /// loaded. Whisper's runtime is process-wide and one-shot, so the selection
    /// only takes effect after a restart (ADR-048).
    /// </summary>
    [ObservableProperty]
    private bool _restartRequired;

    /// <summary>Name of the runtime currently loaded in this process, if any.</summary>
    [ObservableProperty]
    private string? _loadedRuntimeName;

    public RuntimeSettingsViewModel(
        ISettingsService settings,
        IVulkanEnvironmentProvider vulkan,
        IWhisperRuntimeStatus? runtimeStatus = null,
        ILogger<RuntimeSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _vulkan = vulkan;
        // Null in design-time/unit contexts, where no native runtime is ever loaded.
        _runtimeStatus = runtimeStatus;
        _logger = logger ?? NullLogger<RuntimeSettingsViewModel>.Instance;

        RuntimeOptions =
        [
            new(RuntimePreference.Auto, "Auto",
                "Try Vulkan, then fall back to CPU. Recommended.",
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
        var recognized = Enum.TryParse<RuntimePreference>(saved, ignoreCase: true, out var parsed);
        var runtime = recognized ? parsed : RuntimePreference.Auto;
        Apply(runtime);

        // Settings written before ADR-049 can still hold "Cuda". Every reader already
        // degrades to Auto, but rewriting the file keeps the stale value from lingering
        // and showing up in bug reports as a runtime we no longer ship.
        if (!recognized && !string.IsNullOrWhiteSpace(saved))
        {
            _logger.LogInformation(
                "Discarding unsupported runtime preference '{Saved}' — falling back to {Runtime}", saved, runtime);
            await _settings.SetAsync(SettingsKeys.RuntimePreference, runtime.ToString());
        }

        await RefreshAvailabilityAsync();
    }

    /// <summary>
    /// Recomputes the "restart to apply" state for the current selection. Called on
    /// load and after every selection change — the loaded runtime itself can also
    /// appear mid-session, once the first model load happens.
    /// </summary>
    private void RefreshRestartState()
    {
        LoadedRuntimeName = _runtimeStatus?.LoadedRuntimeName;
        RestartRequired = _runtimeStatus?.RequiresRestartFor(SelectedRuntime) ?? false;
    }

    private async Task RefreshAvailabilityAsync()
    {
        var vulkan = await _vulkan.GetAsync();
        VulkanLoaderMissing = !vulkan.HasVulkanLoader;

        foreach (var item in RuntimeOptions)
        {
            switch (item.Type)
            {
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

        RefreshRestartState();
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

        RefreshRestartState();
    }

    [RelayCommand]
    private void OpenVulkanSdkLink()
    {
        _logger.LogInformation("Opening Vulkan SDK page: {Url}", VulkanSdkUrl);
        Process.Start(new ProcessStartInfo(VulkanSdkUrl) { UseShellExecute = true });
    }
}
