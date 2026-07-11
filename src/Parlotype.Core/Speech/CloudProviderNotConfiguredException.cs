namespace Parlotype.Core.Speech;

/// <summary>
/// Thrown when a cloud speech engine is selected but cannot start because its
/// required configuration (the API key) is missing. Derives from
/// <see cref="InvalidOperationException"/> so callers that handle generic
/// initialization failures keep working, while the UI can catch this type
/// specifically to route the user to the cloud-provider settings
/// (ADR-043 amendment; same pattern as <see cref="RuntimeUnavailableException"/>).
/// </summary>
public sealed class CloudProviderNotConfiguredException : InvalidOperationException
{
    /// <summary>The cloud engine that is missing configuration.</summary>
    public SpeechEngine Engine { get; }

    public CloudProviderNotConfiguredException(SpeechEngine engine, string message)
        : base(message)
    {
        Engine = engine;
    }
}
