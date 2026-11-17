namespace Parlotype.Core.Speech;

/// <summary>
/// Which engine an installation uses before the user has chosen one.
/// </summary>
/// <remarks>
/// <para>
/// Parakeet everywhere it can run (ADR-042). The one place it cannot is macOS
/// older than 14.4: the ONNX Runtime that sherpa-onnx ships for macOS links
/// against a CoreML class (<c>MLComputePlan</c>) that first appeared in 14.4, so
/// the library does not load at all on an older system. Those Macs start on
/// Whisper instead, whose natives do load there (ADR-077).
/// </para>
/// <para>
/// Every reader of <see cref="Settings.SettingsKeys.SpeechEngine"/> resolves an
/// unset or unparsable value through <see cref="Parse"/>, so the pipeline, the
/// recognizer factory and the settings pages cannot disagree about the default.
/// </para>
/// </remarks>
public static class SpeechEngineDefaults
{
    /// <summary>First macOS release whose CoreML has what sherpa-onnx's ONNX Runtime needs.</summary>
    private const int ParakeetMinMacOsMajor = 14;
    private const int ParakeetMinMacOsMinor = 4;

    /// <summary>
    /// False only on macOS older than 14.4, where the Parakeet runtime cannot
    /// be loaded. True on Windows, Linux and current macOS.
    /// </summary>
    public static bool IsParakeetAvailable { get; } =
        !OperatingSystem.IsMacOS() ||
        OperatingSystem.IsMacOSVersionAtLeast(ParakeetMinMacOsMajor, ParakeetMinMacOsMinor);

    /// <summary>The engine used when none is stored, on this operating system.</summary>
    public static SpeechEngine Default => PinnedDefault ?? For(IsParakeetAvailable);

    /// <summary>
    /// Test seam. When set, <see cref="Default"/> answers this instead of
    /// following the operating system, so a suite that asserts "unset means
    /// Parakeet" means the same thing on an old Mac as everywhere else. Set
    /// once, before any test runs; never used by the app.
    /// </summary>
    internal static SpeechEngine? PinnedDefault { get; set; }

    /// <summary>The default for a system that can (or cannot) run Parakeet. Split out so both answers are testable anywhere.</summary>
    public static SpeechEngine For(bool parakeetAvailable) =>
        parakeetAvailable ? SpeechEngine.Parakeet : SpeechEngine.Whisper;

    /// <summary>
    /// Reads a stored <see cref="Settings.SettingsKeys.SpeechEngine"/> value.
    /// Unset, blank and unparsable values all mean <see cref="Default"/>.
    /// </summary>
    public static SpeechEngine Parse(string? stored) =>
        Enum.TryParse<SpeechEngine>(stored, ignoreCase: true, out var parsed) ? parsed : Default;
}
