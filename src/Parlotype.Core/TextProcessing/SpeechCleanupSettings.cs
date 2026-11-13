using Parlotype.Core.Settings;

namespace Parlotype.Core.TextProcessing;

/// <summary>
/// The persisted speech cleanup configuration, read in one place so the audio
/// pipeline and the settings page can never disagree about a default.
/// </summary>
public sealed record SpeechCleanupSettings(bool Enabled, SpeechCleanupOptions Options)
{
    /// <summary>
    /// Reads the cleanup keys. Anything unset or unparsable falls back to off
    /// and to <see cref="SpeechCleanupOptions.Default"/>.
    /// </summary>
    public static async Task<SpeechCleanupSettings> LoadAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var enabledStr = await settings.GetAsync<string>(SettingsKeys.SpeechCleanupEnabled, cancellationToken);
        var enabled = bool.TryParse(enabledStr, out var e) && e;

        var defaults = SpeechCleanupOptions.Default;
        var options = new SpeechCleanupOptions(
            await ReadEnumAsync(settings, SettingsKeys.SpeechCleanupStyling, defaults.Styling, cancellationToken),
            await ReadEnumAsync(settings, SettingsKeys.SpeechCleanupStructure, defaults.Structure, cancellationToken),
            await ReadEnumAsync(settings, SettingsKeys.SpeechCleanupContext, defaults.Context, cancellationToken));

        return new SpeechCleanupSettings(enabled, options);
    }

    private static async Task<T> ReadEnumAsync<T>(
        ISettingsService settings, string key, T fallback, CancellationToken cancellationToken)
        where T : struct, Enum
    {
        var raw = await settings.GetAsync<string>(key, cancellationToken);

        // Enum.TryParse accepts any number as an undefined member ("7"), which
        // would later throw inside the prompt builder; IsDefined closes that gap.
        return Enum.TryParse<T>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : fallback;
    }
}
