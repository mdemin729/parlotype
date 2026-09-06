using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform.Startup;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Builds a fully-wired <see cref="SettingsWindowViewModel"/> over mocks. Shared
/// because the shell needs every section view model, and more than one test class
/// wants the whole thing rather than a single section.
/// </summary>
public static class SettingsWindowViewModelFactory
{
    public static SettingsWindowViewModel Build(MockSettingsService? settings = null)
    {
        settings ??= new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(new MicrophoneInfo("m1", "Mic 1", true));
        var vulkan = new MockVulkanEnvironmentProvider();

        var engine = new SpeechEngineSettingsViewModel(settings);
        var mic = new MicrophoneSettingsViewModel(enumerator, settings);
        var silence = new SilenceTimeoutSettingsViewModel(settings);
        var model = new WhisperModelSettingsViewModel(settings);
        var runtime = new RuntimeSettingsViewModel(settings, vulkan);
        var whisperOutput = new WhisperOutputSettingsViewModel(settings);
        var language = new LanguageSelectionSettingsViewModel(
            new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService()));
        var gemma4Model = new Gemma4ModelSettingsViewModel(settings);
        var parakeetModel = new ParakeetModelSettingsViewModel(settings);
        var cloudProviders = new CloudProviderSettingsViewModel(settings, new MockSecretStore());
        var prompts = new PromptSettingsViewModel(new MockPromptTemplateRegistry());
        var llamaCpp = new LlamaCppSettingsViewModel(settings);
        var hotkey = new HotkeySettingsViewModel(hotkeyService: null, settings);
        var theme = new ThemeSettingsViewModel(settings);
        var interfaceLanguage = new InterfaceLanguageSettingsViewModel(new UiLanguageService(settings));
        var startup = new StartupSettingsViewModel(new LaunchAtLoginCoordinator(
            settings,
            new MockLaunchAtLoginService(),
            NullLogger<LaunchAtLoginCoordinator>.Instance));
        var updates = new UpdateSettingsViewModel(settings, new MockUpdateService());
        var data = new DataSettingsViewModel(settings);
        var help = new HelpSettingsViewModel(new MockOnboardingService(), hotkeyService: null);

        return new SettingsWindowViewModel(
            engine, mic, silence, model, runtime, whisperOutput, language, gemma4Model, parakeetModel,
            cloudProviders, prompts, llamaCpp, hotkey, theme, interfaceLanguage, startup, updates, data, help);
    }
}
