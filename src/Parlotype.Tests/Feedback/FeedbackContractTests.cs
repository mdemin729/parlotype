using Parlotype.Core.Feedback;
using Parlotype.Platform.Feedback;
using Xunit;

namespace Parlotype.Tests.Feedback;

/// <summary>
/// The pure parts of the feedback contract (ADR-070): field rules mirrored from the
/// server, the fallback URLs, and the OS description.
/// </summary>
public class FeedbackContractTests
{
    [Fact]
    public void Limits_MatchTheContract()
    {
        Assert.Equal(5000, FeedbackLimits.MessageMaxLength);
        Assert.Equal(254, FeedbackLimits.EmailMaxLength);
        Assert.Equal(64, FeedbackLimits.AppVersionMaxLength);
        Assert.Equal(128, FeedbackLimits.OperatingSystemMaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("someone@example.com")]
    [InlineData("  first.last+tag@sub.example.co.uk ")]
    public void Email_AcceptsBlankAndWellFormed(string? email) =>
        Assert.True(FeedbackLimits.IsAcceptableEmail(email));

    [Theory]
    [InlineData("someone")]
    [InlineData("someone@example")]
    [InlineData("some one@example.com")]
    [InlineData("a@b@c.com")]
    [InlineData("<a>@example.com")]
    [InlineData("a@exa;mple.com")]
    public void Email_RejectsWhatTheServerRejects(string email) =>
        Assert.False(FeedbackLimits.IsAcceptableEmail(email));

    [Fact]
    public void Email_RejectsOverTheLengthLimit()
    {
        var local = new string('a', 64);
        var tooLong = $"{local}@{new string('b', 190)}.com";

        Assert.True(tooLong.Length > FeedbackLimits.EmailMaxLength);
        Assert.False(FeedbackLimits.IsAcceptableEmail(tooLong));
    }

    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("  padded  ", "padded")]
    [InlineData("tab\tkept", "tab\tkept")]
    [InlineData("bell\u0007 nul\u0000 del\u007F c1\u0085 gone", "bell nul del c1 gone")]
    public void Message_IsNormalizedTheWayTheServerStoresIt(string input, string expected) =>
        Assert.Equal(expected, FeedbackLimits.NormalizeMessage(input));

    [Theory]
    [InlineData(null, false)]
    [InlineData(" \n ", false)]
    [InlineData("x", true)]
    public void Message_IsSendableOnlyWithText(string? message, bool expected) =>
        Assert.Equal(expected, FeedbackLimits.IsSendableMessage(message));

    [Fact]
    public void Message_IsSendableUpToTheLimit_AndNotBeyond()
    {
        Assert.True(FeedbackLimits.IsSendableMessage(new string('a', 5000)));
        Assert.False(FeedbackLimits.IsSendableMessage(new string('a', 5001)));
        // Trimmed first, as the server does.
        Assert.True(FeedbackLimits.IsSendableMessage("  " + new string('a', 5000) + "  "));
    }

    [Theory]
    [InlineData("0.6.0", "0.6.0")]
    [InlineData("0.6.0-beta.1+abc123", "0.6.0-beta.1+abc123")]
    [InlineData("0.6.0 (dev)", "0.6.0dev")]
    [InlineData(null, "unknown")]
    [InlineData("()", "unknown")]
    public void AppVersion_IsReducedToTheContractsAlphabet(string? input, string expected) =>
        Assert.Equal(expected, FeedbackLimits.NormalizeAppVersion(input));

    [Fact]
    public void AppVersion_IsCappedAtSixtyFourCharacters()
    {
        var version = "1.0.0+" + new string('f', 100);

        Assert.Equal(64, FeedbackLimits.NormalizeAppVersion(version).Length);
    }

    [Fact]
    public void OperatingSystem_IsOneLineWithinTheLimit()
    {
        var normalized = FeedbackLimits.NormalizeOperatingSystem("Linux\n  5.15\t" + new string('x', 200));

        Assert.DoesNotContain('\n', normalized);
        Assert.DoesNotContain('\t', normalized);
        Assert.StartsWith("Linux 5.15 x", normalized, StringComparison.Ordinal);
        Assert.True(normalized.Length <= FeedbackLimits.OperatingSystemMaxLength);
    }

    [Theory]
    [InlineData(10, 0, 26100, "24H2", "Windows 11 24H2 (10.0.26100)")]
    [InlineData(10, 0, 22000, "21H2", "Windows 11 21H2 (10.0.22000)")]
    [InlineData(10, 0, 19045, "22H2", "Windows 10 22H2 (10.0.19045)")]
    [InlineData(10, 0, 26200, null, "Windows 11 (10.0.26200)")]
    [InlineData(6, 3, 9600, null, "Windows (6.3.9600)")]
    public void Windows_IsDescribedLikeTheContractsExample(int major, int minor, int build, string? display, string expected) =>
        Assert.Equal(expected, OperatingSystemDescription.FormatWindows(new Version(major, minor, build), display));

    [Fact]
    public void CurrentOperatingSystem_IsAValidField()
    {
        var description = OperatingSystemDescription.Current();

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.True(description.Length <= FeedbackLimits.OperatingSystemMaxLength);
        Assert.DoesNotContain('\n', description);
        if (OperatingSystem.IsWindows())
            Assert.StartsWith("Windows", description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "https://parlotype.dev/feedback/")]
    [InlineData(null, "https://parlotype.dev/feedback/")]
    [InlineData("", "https://parlotype.dev/feedback/")]
    [InlineData("de", "https://parlotype.dev/de/feedback/")]
    [InlineData("pt-BR", "https://parlotype.dev/pt/feedback/")]
    [InlineData("RU", "https://parlotype.dev/ru/feedback/")]
    // A language the site lacks still gets its own prefix: the site redirects it.
    [InlineData("mt", "https://parlotype.dev/mt/feedback/")]
    // Anything that is not a plain language code falls back to English.
    [InlineData("../admin", "https://parlotype.dev/feedback/")]
    [InlineData("x1", "https://parlotype.dev/feedback/")]
    public void WebsiteForm_IsTheLanguagesFormWithNothingElseInTheUrl(string? language, string expected)
    {
        var uri = FeedbackLinks.WebsiteForm(language);

        Assert.Equal(expected, uri.AbsoluteUri);
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }

    [Fact]
    public void GitHubIssues_IsTheRepositorysTracker() =>
        Assert.Equal("https://github.com/mdemin729/parlotype/issues", FeedbackLinks.GitHubIssues.AbsoluteUri);

    [Theory]
    [InlineData(FeedbackSendOutcome.Sent, false)]
    [InlineData(FeedbackSendOutcome.InvalidEmail, false)]
    [InlineData(FeedbackSendOutcome.RateLimited, false)]
    [InlineData(FeedbackSendOutcome.Rejected, true)]
    [InlineData(FeedbackSendOutcome.AppPathClosed, true)]
    [InlineData(FeedbackSendOutcome.ServerError, true)]
    [InlineData(FeedbackSendOutcome.Timeout, true)]
    [InlineData(FeedbackSendOutcome.NetworkError, true)]
    public void Fallback_IsOfferedOnlyWhenSendingAgainWouldNotHelp(FeedbackSendOutcome outcome, bool expected) =>
        Assert.Equal(expected, new FeedbackSendResult(outcome).OffersFallback);
}
