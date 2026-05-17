using Microsoft.Extensions.DependencyInjection;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.Speech.LlamaServer;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Hotkeys;
using Parlotype.Platform.Settings;
using Parlotype.Platform.Speech;
using Parlotype.Platform.Speech.LlamaServer;

namespace Parlotype.Platform;

/// <summary>Registers all platform service implementations into the DI container.</summary>
public static class PlatformServiceExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService, WasapiAudioCaptureService>();
        services.AddSingleton<IVadService, SileroVadService>();
        // Both recognizers are registered as concrete singletons.
        // DelegatingSpeechRecognizer reads the SpeechEngine setting at
        // InitializeAsync time and forwards to the correct one.
        services.AddSingleton<WhisperSpeechRecognizer>();
        services.AddSingleton<LlamaCppSpeechRecognizer>();
        // The recognizer also surfaces ILlamaCppServerLifecycle so the
        // installer can stop the sidecar before deleting its files
        // (Windows file-lock release on uninstall/switch).
        services.AddSingleton<ILlamaCppServerLifecycle>(sp => sp.GetRequiredService<LlamaCppSpeechRecognizer>());
        services.AddSingleton<ILlamaServerRegistry, JsonLlamaServerRegistry>();
        services.AddSingleton<SpeechRecognizerFactory>();
        services.AddSingleton<ISpeechRecognizer, DelegatingSpeechRecognizer>();
        services.AddSingleton<IAudioPipeline, AudioPipelineService>();
        services.AddSingleton<IAudioLevelProvider>(sp =>
            (AudioPipelineService)sp.GetRequiredService<IAudioPipeline>());
        services.AddSingleton<IGlobalHotkeyService, SharpHookHotkeyService>();
        services.AddSingleton<IMicrophoneEnumerator, WasapiMicrophoneEnumerator>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromHours(1) });
        services.AddSingleton<StreamingFileDownloader>();
        services.AddSingleton<HttpModelDownloadService>();
        services.AddSingleton<Gemma4ModelDownloadService>();
        services.AddSingleton<ILlamaServerCatalog, GitHubLlamaServerCatalog>();
        services.AddSingleton<ILlamaServerInstaller, LlamaServerInstaller>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<INvidiaEnvironmentProvider, WindowsNvidiaEnvironmentProvider>();
            services.AddSingleton<IVulkanEnvironmentProvider, WindowsVulkanEnvironmentProvider>();
        }
        else
        {
            services.AddSingleton<INvidiaEnvironmentProvider, NoOpNvidiaEnvironmentProvider>();
            services.AddSingleton<IVulkanEnvironmentProvider, NoOpVulkanEnvironmentProvider>();
        }

        return services;
    }
}
