using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Whisper.net.LibraryLoader;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Configures Whisper.net's <see cref="RuntimeOptions.RuntimeLibraryOrder"/> based on
/// the user's <see cref="RuntimePreference"/> setting.
/// Must be called <b>before</b> any <see cref="Whisper.net.WhisperFactory"/> is created.
/// </summary>
internal static class WhisperRuntimeBootstrap
{
    private static int _initialized; // 0 = false, 1 = true (for Interlocked)

    /// <summary>Whether <see cref="Initialize"/> has already been called.</summary>
    public static bool IsInitialized => Volatile.Read(ref _initialized) == 1;

    /// <summary>
    /// The runtime library that Whisper.net actually loaded after the first factory creation.
    /// Returns <c>null</c> until a <see cref="Whisper.net.WhisperFactory"/> has been created.
    /// </summary>
    public static RuntimeLibrary? LoadedRuntime => RuntimeOptions.LoadedLibrary;

    /// <summary>
    /// Sets <see cref="RuntimeOptions.RuntimeLibraryOrder"/> according to the given
    /// <paramref name="preference"/>. This is idempotent — only the first call takes effect.
    /// </summary>
    public static void Initialize(RuntimePreference preference, ILogger logger)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
        {
            logger.LogDebug("WhisperRuntimeBootstrap.Initialize called again — already initialized, skipping");
            return;
        }

        RuntimeOptions.RuntimeLibraryOrder = preference switch
        {
            RuntimePreference.Cpu => [RuntimeLibrary.Cpu],
            // Auto: try CUDA first, then fall back to CPU
            _ => [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu],
        };

        logger.LogInformation(
            "Whisper runtime order configured for {Preference}: [{Order}]",
            preference,
            string.Join(", ", RuntimeOptions.RuntimeLibraryOrder));
    }

    /// <summary>
    /// Reads the <see cref="SettingsKeys.RuntimePreference"/> setting and calls
    /// <see cref="Initialize(RuntimePreference, ILogger)"/>.
    /// Defaults to <see cref="RuntimePreference.Auto"/> when the setting is missing or invalid.
    /// </summary>
    public static async Task EnsureInitializedAsync(ISettingsService settings, ILogger logger)
    {
        if (IsInitialized)
            return;

        var saved = await settings.GetAsync<string>(SettingsKeys.RuntimePreference);

        var preference = Enum.TryParse<RuntimePreference>(saved, ignoreCase: true, out var parsed)
            ? parsed
            : RuntimePreference.Auto;

        Initialize(preference, logger);
    }

    /// <summary>
    /// Resets the initialization flag so <see cref="Initialize"/> can be called again.
    /// <b>For testing only</b> — do not call in production code.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void Reset()
    {
        Volatile.Write(ref _initialized, 0);
    }
}
