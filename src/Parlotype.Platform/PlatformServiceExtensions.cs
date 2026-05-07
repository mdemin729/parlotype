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
        services.AddSingleton<ISpeechRecognizer, WhisperSpeechRecognizer>();
        // Whisper runtime bootstrap (CUDA / Vulkan / CPU) is handled lazily inside
        // WhisperSpeechRecognizer.InitializeAsync, before any WhisperFactory
        // is created. No eager initialization is needed here.
        services.AddSingleton<IAudioPipeline, AudioPipelineService>();
        services.AddSingleton<IAudioLevelProvider>(sp =>
            (AudioPipelineService)sp.GetRequiredService<IAudioPipeline>());
        services.AddSingleton<IGlobalHotkeyService, SharpHookHotkeyService>();
        services.AddSingleton<IMicrophoneEnumerator, WasapiMicrophoneEnumerator>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromHours(1) });
        services.AddSingleton<HttpModelDownloadService>();

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
