using Microsoft.Extensions.DependencyInjection;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Hotkeys;
using Parlotype.Platform.Speech;

namespace Parlotype.Platform;

/// <summary>Registers all platform service implementations into the DI container.</summary>
public static class PlatformServiceExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService, WasapiAudioCaptureService>();
        services.AddSingleton<ISpeechRecognizer, WhisperSpeechRecognizer>();
        services.AddSingleton<IGlobalHotkeyService, SharpHookHotkeyService>();
        return services;
    }
}
