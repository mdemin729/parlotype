namespace Parlotype.Core.Speech;

/// <summary>
/// Resolves the persisted source-language code into the concrete code the
/// recognizer should use. The <see cref="LanguageCatalog.KeyboardLayoutCode"/>
/// sentinel becomes the detected keyboard-layout language; everything else
/// passes through unchanged. Pure so both pipeline call sites (WhisperOptions
/// and the Gemma prompt) share one tested fallback policy.
/// </summary>
public static class SourceLanguageResolver
{
    /// <summary>
    /// Resolves <paramref name="sourceCode"/> against the detected keyboard
    /// layout. Falls back to <see cref="LanguageCatalog.AutoDetectCode"/> when
    /// the code is blank, when the keyboard sentinel is set but detection
    /// returned nothing, or when the detected language is not in
    /// <paramref name="supported"/> (pass <c>null</c> to skip that check).
    /// </summary>
    public static string Resolve(
        string? sourceCode,
        KeyboardLayoutInfo? detectedLayout,
        IReadOnlyList<LanguageInfo>? supported = null)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            return LanguageCatalog.AutoDetectCode;

        if (!LanguageCatalog.IsKeyboardLayout(sourceCode))
            return sourceCode;

        var detected = detectedLayout?.LanguageCode;
        if (string.IsNullOrWhiteSpace(detected))
            return LanguageCatalog.AutoDetectCode;

        if (supported is not null
            && !supported.Any(l => string.Equals(l.Code, detected, StringComparison.OrdinalIgnoreCase)))
        {
            return LanguageCatalog.AutoDetectCode;
        }

        return detected;
    }
}
