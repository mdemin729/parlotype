using Microsoft.Extensions.DependencyInjection;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Hotkeys;
using Parlotype.Platform.Settings;
using Parlotype.Platform.Speech;

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
