namespace Parlotype.Core.Feedback;

/// <summary>
/// Where feedback goes (ADR-070): the API, and the two places the dialog hands the user
/// over to when the app cannot deliver a message itself.
/// </summary>
public static class FeedbackLinks
{
    /// <summary>The production endpoint. Release builds always use it.</summary>
    public static Uri Endpoint { get; } = new("https://parlotype.dev/api/feedback");

    /// <summary>The second fallback: the repository's issue tracker.</summary>
    public static Uri GitHubIssues { get; } = new("https://github.com/mdemin729/parlotype/issues");

    /// <summary>
    /// The website's feedback form in <paramref name="uiLanguage"/>:
    /// <c>https://parlotype.dev/de/feedback/</c>, or <c>https://parlotype.dev/feedback/</c>
    /// for English and for anything that is not a plain language code.
    /// </summary>
    /// <remarks>
    /// No list of the site's languages here, deliberately: the site redirects every app
    /// language it lacks to its English form. The URL carries the language and nothing
    /// else — never the message, email or diagnostics, which would end up in browser
    /// history and analytics.
    /// </remarks>
    public static Uri WebsiteForm(string? uiLanguage)
    {
        var language = (uiLanguage ?? "").Split('-')[0].ToLowerInvariant();

        var isPlainCode = language.Length is 2 or 3 && language.All(c => c is >= 'a' and <= 'z');
        if (!isPlainCode || language == "en")
            return new Uri("https://parlotype.dev/feedback/");

        return new Uri($"https://parlotype.dev/{language}/feedback/");
    }
}
