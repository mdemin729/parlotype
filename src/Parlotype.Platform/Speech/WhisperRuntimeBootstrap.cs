using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;

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
    /// Whether the runtime library Whisper.net loaded satisfies <paramref name="preference"/>.
    /// A <c>null</c> <paramref name="loaded"/> (nothing loaded yet) is always satisfied, and
    /// <see cref="RuntimePreference.Auto"/> accepts whatever won the fallback chain.
    /// </summary>
    public static bool IsSatisfiedBy(RuntimePreference preference, RuntimeLibrary? loaded)
    {
        if (loaded is not { } library)
            return true;

        return preference switch
        {
            RuntimePreference.Cuda => library == RuntimeLibrary.Cuda,
            RuntimePreference.Vulkan => library == RuntimeLibrary.Vulkan,
            // Whisper.net picks between the AVX and no-AVX CPU builds itself —
            // both honour a Cpu preference.
            RuntimePreference.Cpu => library is RuntimeLibrary.Cpu or RuntimeLibrary.CpuNoAvx,
            _ => true,
        };
    }

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

        // Bridge Whisper.net internal diagnostics (CUDA probing, DLL loading) to our logger.
        // Note: Whisper.net 1.9.0 has an inverted GgmlLogLevel enum (Error=2 matches native INFO=2),
        // so native informational messages arrive as WhisperLogLevel.Error. We remap accordingly.
        LogProvider.AddLogger((level, message) =>
        {
            var logLevel = level switch
            {
                WhisperLogLevel.Error => LogLevel.Information,  // native INFO (ggml value 2) misclassified
                WhisperLogLevel.Warning => LogLevel.Debug,      // native WARN (ggml value 1) misclassified
                WhisperLogLevel.Info => LogLevel.Trace,          // native value 4 — not emitted by ggml
                WhisperLogLevel.Debug => LogLevel.Debug,
                _ => LogLevel.Trace,
            };
            logger.Log(logLevel, "[Whisper.net] {Message}", message);
        });

        RuntimeOptions.RuntimeLibraryOrder = preference switch
        {
            RuntimePreference.Cpu => [RuntimeLibrary.Cpu],
            // Strict modes: no fallback. WhisperSpeechRecognizer pre-checks the
            // environment and surfaces a RuntimeUnavailableException if the chosen
            // backend isn't usable.
            RuntimePreference.Cuda => [RuntimeLibrary.Cuda],
            RuntimePreference.Vulkan => [RuntimeLibrary.Vulkan],
            // Auto: try CUDA, then Vulkan, then fall back to CPU.
            _ => [RuntimeLibrary.Cuda, RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu],
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
