using Microsoft.Extensions.DependencyInjection;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Updates;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Hotkeys;
using Parlotype.Platform.Settings;
using Parlotype.Platform.Speech;
using Parlotype.Platform.LlamaServer;
using Parlotype.Platform.Updates;

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
        // Read-only view of the process-wide Whisper runtime latch, so Settings can
        // tell the user a restart is pending instead of failing at record time.
        services.AddSingleton<IWhisperRuntimeStatus, WhisperRuntimeStatus>();
        services.AddSingleton<LlamaCppSpeechRecognizer>();
        services.AddSingleton<ParakeetSpeechRecognizer>();
        // Opt-in cloud providers (ADR-032, bring-your-own-key). Registered as
        // concrete singletons like the local engines above; SpeechRecognizerFactory
        // resolves the one matching the configured SpeechEngine setting.
        services.AddSingleton<OpenAiCompatibleSpeechRecognizer>();
        services.AddSingleton<XaiGrokSpeechRecognizer>();
        // The recognizer also surfaces ILlamaCppServerLifecycle so the
        // installer can stop the sidecar before deleting its files
        // (Windows file-lock release on uninstall/switch).
        services.AddSingleton<ILlamaCppServerLifecycle>(sp => sp.GetRequiredService<LlamaCppSpeechRecognizer>());
        services.AddSingleton<ILlamaServerRegistry, JsonLlamaServerRegistry>();
        services.AddSingleton<IPromptTemplateRegistry, JsonPromptTemplateRegistry>();
        services.AddSingleton<SpeechRecognizerFactory>();
        services.AddSingleton<ISpeechRecognizer, DelegatingSpeechRecognizer>();
        services.AddSingleton<IAudioPipeline, AudioPipelineService>();
        services.AddSingleton<IAudioLevelProvider>(sp =>
            (AudioPipelineService)sp.GetRequiredService<IAudioPipeline>());
        services.AddSingleton<IGlobalHotkeyService, SharpHookHotkeyService>();
        services.AddSingleton<IMicrophoneEnumerator, WasapiMicrophoneEnumerator>();
        // Single source of truth for every path the app writes to. Must resolve
        // outside the Velopack pack folder, which is wiped on uninstall (ADR-053).
        services.AddSingleton<IAppPaths>(AppPaths.Default);
        // The only outbound request Parlotype makes in local mode. Opt-out lives
        // at SettingsKeys.UpdatesCheckAutomatically (ADR-053).
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        // API keys for the opt-in cloud providers live outside settings.json,
        // encrypted at rest where the OS supports it (ADR-032).
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        // Deliberately separate from ISettingsService's settings.json — window
        // chrome state (position) changes far more often (every drag) and must
        // never share a file/lock with user-configured settings (ADR-040).
        services.AddSingleton<IWindowStateService, JsonWindowStateService>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromHours(1) });
        services.AddSingleton<StreamingFileDownloader>();
        services.AddSingleton<HttpModelDownloadService>();
        services.AddSingleton<Gemma4ModelDownloadService>();
        services.AddSingleton<ParakeetModelDownloadService>();
        // Headless default; the Desktop app re-registers IParakeetModelProvider
        // with a dialog-based wrapper (last registration wins), same pattern as
        // ILlamaServerInstaller above.
        services.AddSingleton<IParakeetModelProvider>(sp => sp.GetRequiredService<ParakeetModelDownloadService>());
        services.AddSingleton<ILlamaServerCatalog, GitHubLlamaServerCatalog>();
        // Register the installer as a concrete singleton too so a Desktop
        // wrapper can inject it directly while still resolving as
        // ILlamaServerInstaller for callers that just want the headless impl.
        services.AddSingleton<LlamaServerInstaller>();
        services.AddSingleton<ILlamaServerInstaller>(sp => sp.GetRequiredService<LlamaServerInstaller>());

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<INvidiaEnvironmentProvider, WindowsNvidiaEnvironmentProvider>();
            services.AddSingleton<IVulkanEnvironmentProvider, WindowsVulkanEnvironmentProvider>();
            services.AddSingleton<IKeyboardLayoutService, Win32KeyboardLayoutService>();
        }
        else
        {
            services.AddSingleton<INvidiaEnvironmentProvider, NoOpNvidiaEnvironmentProvider>();
            services.AddSingleton<IVulkanEnvironmentProvider, NoOpVulkanEnvironmentProvider>();
            services.AddSingleton<IKeyboardLayoutService, NoOpKeyboardLayoutService>();
        }

        return services;
    }
}
