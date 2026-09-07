using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Localized presentation for cloud-provider failures (ADR-064 amendment).
/// </summary>
/// <remarks>
/// <para>
/// The cloud exceptions carry their reason as an enum plus the parts that vary
/// (provider, HTTP status, the provider's own error text). Their
/// <c>Message</c> stays the invariant English form the log lines write — same
/// split as <see cref="Settings.HotkeyText"/> and Core's
/// <c>HotkeyGesture.DisplayString</c>: Core writes for machines, this writes
/// for the window.
/// </para>
/// <para>
/// Every format puts the provider name first, followed by a colon or a verb.
/// That is deliberate: the slot then never needs a grammatical case, so Russian
/// and Spanish can translate the provider name without the sentence around it
/// having to agree with it.
/// </para>
/// <para>
/// The provider's own error text is <em>not</em> translated. It arrives already
/// written, in whatever language the provider chose, and rewording it would
/// misquote them.
/// </para>
/// </remarks>
public static class CloudErrorText
{
    /// <summary>The provider's name as error messages address it.</summary>
    public static string Provider(SpeechEngine engine) => engine switch
    {
        SpeechEngine.XaiGrok => Strings.Cloud_ProviderName_XaiGrok,
        _ => Strings.Cloud_ProviderName_OpenAiCompatible,
    };

    /// <summary>Why a cloud engine could not start.</summary>
    public static string NotConfigured(CloudProviderNotConfiguredException ex)
    {
        var provider = Provider(ex.Engine);

        return ex.Error == CloudConfigurationError.InvalidBaseUrl && ex.UrlFailure is { } failure
            ? Strings.Format_Cloud_NotConfigured_InvalidBaseUrlFormat(provider, BaseUrl(failure))
            : Strings.Format_Cloud_NotConfigured_MissingKeyFormat(provider);
    }

    /// <summary>
    /// Why a base URL was refused, as a sentence fragment. Shown on its own
    /// under the field in Settings and embedded in <see cref="NotConfigured"/>.
    /// </summary>
    public static string BaseUrl(CloudBaseUrlFailure failure) => failure.Error switch
    {
        CloudBaseUrlError.PlainHttpNotLoopback => Strings.Cloud_BaseUrl_PlainHttp,
        CloudBaseUrlError.UnsupportedScheme =>
            Strings.Format_Cloud_BaseUrl_UnsupportedSchemeFormat(failure.Scheme ?? string.Empty),
        _ => Strings.Cloud_BaseUrl_NotAbsolute,
    };

    /// <summary>Why a cloud transcription request failed.</summary>
    public static string Transcription(CloudSpeechTranscriptionException ex)
    {
        var provider = Provider(ex.Engine);
        // An HTTP status is a protocol number, not a quantity — it stays in
        // Latin digits whatever the interface language is.
        var status = ex.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var detail = ex.ProviderMessage ?? string.Empty;

        return ex.Kind switch
        {
            CloudSpeechErrorKind.KeyRejected =>
                Strings.Format_Cloud_Error_KeyRejectedFormat(provider, status, detail),
            CloudSpeechErrorKind.QuotaExceeded =>
                Strings.Format_Cloud_Error_QuotaExceededFormat(provider, detail),
            CloudSpeechErrorKind.RateLimited =>
                Strings.Format_Cloud_Error_RateLimitedFormat(provider, detail),
            CloudSpeechErrorKind.ProviderUnavailable =>
                Strings.Format_Cloud_Error_ProviderUnavailableFormat(provider, status, detail),
            _ =>
                Strings.Format_Cloud_Error_OtherFormat(provider, status, detail),
        };
    }
}
