using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Points the managed ONNX Runtime at an older native build on Intel Macs
/// whose macOS cannot load the one sherpa-onnx ships (ADR-077).
/// </summary>
/// <remarks>
/// <para>
/// Silero VAD runs before every engine, so a Mac that cannot load an ONNX
/// Runtime cannot dictate at all, even with Whisper or a cloud engine selected.
/// The runtime sherpa-onnx bundles for macOS needs macOS 14.4; Microsoft's
/// 1.18.1, the version SileroVad targets, needs macOS 11. The build copies the
/// latter to <see cref="RelativePath"/> and this class loads it by path.
/// </para>
/// <para>
/// It applies only where the bundled runtime cannot load. Everywhere else
/// nothing is registered and the process keeps the single shared runtime it
/// has on Windows.
/// </para>
/// </remarks>
internal static class LegacyOnnxRuntime
{
    /// <summary>Where the build puts the fallback library, relative to the app's base directory.</summary>
    internal const string RelativePath = "onnxruntime-legacy/osx-x64/libonnxruntime.dylib";

    /// <summary>The name the managed ONNX Runtime passes to the native loader.</summary>
    private const string OnnxRuntimeLibraryName = "onnxruntime";

    private static int _configured; // 0 = false, 1 = true (for Interlocked)

    /// <summary>
    /// Whether the fallback is the right library for a system. Split out so the
    /// rule is testable on any machine.
    /// </summary>
    internal static bool IsNeeded(bool isMacOs, Architecture architecture, bool bundledRuntimeLoads) =>
        isMacOs && architecture == Architecture.X64 && !bundledRuntimeLoads;

    /// <summary>
    /// Registers the fallback if this system needs it. Idempotent, and must run
    /// before the first ONNX Runtime call: a native library is resolved once.
    /// </summary>
    public static void EnsureConfigured(ILogger logger)
    {
        if (Interlocked.CompareExchange(ref _configured, 1, 0) != 0)
            return;

        // Parakeet's availability is exactly "the bundled runtime loads".
        if (!IsNeeded(OperatingSystem.IsMacOS(), RuntimeInformation.ProcessArchitecture,
                bundledRuntimeLoads: SpeechEngineDefaults.IsParakeetAvailable))
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, RelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning(
                "This macOS version needs the fallback ONNX Runtime, but {Path} is missing. " +
                "Voice activity detection will fail to load.", path);
            return;
        }

        try
        {
            var managed = Assembly.Load(new AssemblyName("Microsoft.ML.OnnxRuntime"));
            NativeLibrary.SetDllImportResolver(managed, (name, _, _) =>
                name == OnnxRuntimeLibraryName ? NativeLibrary.Load(path) : IntPtr.Zero);

            logger.LogInformation(
                "Using the fallback ONNX Runtime for voice activity detection (macOS older than 14.4)");
        }
        catch (Exception ex)
        {
            // A resolver may be set only once per assembly; if something else
            // got there first, or the assembly is absent, leave loading alone.
            logger.LogWarning(ex, "Could not register the fallback ONNX Runtime");
        }
    }
}
