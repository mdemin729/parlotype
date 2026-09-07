namespace Parlotype.Core.Speech;

/// <summary>What a cloud engine is missing before it can start.</summary>
public enum CloudConfigurationError
{
    /// <summary>No API key has been stored for the provider.</summary>
    MissingApiKey,

    /// <summary>The configured base URL was refused — see <see cref="CloudBaseUrlFailure"/>.</summary>
    InvalidBaseUrl
}

/// <summary>
/// Thrown when a cloud speech engine is selected but cannot start because its
/// required configuration (the API key) is missing. Derives from
/// <see cref="InvalidOperationException"/> so callers that handle generic
/// initialization failures keep working, while the UI can catch this type
/// specifically to route the user to the cloud-provider settings
/// (ADR-043 amendment; same pattern as <see cref="RuntimeUnavailableException"/>).
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> is the invariant English form the logs
/// write. The dialog builds its own sentence from <see cref="Engine"/>,
/// <see cref="Error"/> and <see cref="UrlFailure"/> so it can be translated
/// (ADR-064 amendment).
/// </remarks>
public sealed class CloudProviderNotConfiguredException : InvalidOperationException
{
    /// <summary>The cloud engine that is missing configuration.</summary>
    public SpeechEngine Engine { get; }

    /// <summary>Which piece of configuration is at fault.</summary>
    public CloudConfigurationError Error { get; }

    /// <summary>Set when <see cref="Error"/> is <see cref="CloudConfigurationError.InvalidBaseUrl"/>.</summary>
    public CloudBaseUrlFailure? UrlFailure { get; }

    public CloudProviderNotConfiguredException(
        SpeechEngine engine,
        CloudConfigurationError error,
        string message,
        CloudBaseUrlFailure? urlFailure = null)
        : base(message)
    {
        Engine = engine;
        Error = error;
        UrlFailure = urlFailure;
    }
}
