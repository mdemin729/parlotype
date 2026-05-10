using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Creates the appropriate <see cref="ISpeechRecognizer"/> based on the
/// configured <see cref="SpeechEngine"/> setting.
/// </summary>
public sealed class SpeechRecognizerFactory
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly ILogger<SpeechRecognizerFactory> _logger;

    public SpeechRecognizerFactory(
        IServiceProvider services,
        ISettingsService settings,
        ILogger<SpeechRecognizerFactory> logger)
    {
        _services = services;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Reads the current <see cref="SettingsKeys.SpeechEngine"/> setting and
    /// returns the matching recognizer instance from DI.
    /// </summary>
    public async Task<ISpeechRecognizer> GetRecognizerAsync(CancellationToken cancellationToken = default)
    {
        var engineStr = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine);
        var engine = Enum.TryParse<SpeechEngine>(engineStr, ignoreCase: true, out var parsed)
            ? parsed
            : SpeechEngine.Whisper;

        _logger.LogInformation("Resolved speech engine: {Engine}", engine);

        return engine switch
        {
            SpeechEngine.Gemma4 => _services.GetRequiredService<LlamaCppSpeechRecognizer>(),
            _ => _services.GetRequiredService<WhisperSpeechRecognizer>(),
        };
    }
}
