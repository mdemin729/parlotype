using System.Text;
using System.Text.RegularExpressions;

namespace Parlotype.Core.Feedback;

/// <summary>
/// The feedback API's field rules (ADR-070), enforced on this side so the server never
/// has to reject something the user wrote.
/// </summary>
/// <remarks>
/// These mirror <c>feedbackLimits</c> and the validators in the website's
/// <c>worker/feedback.ts</c>. The contract promises the server will never tighten them,
/// so the only way these can drift is by being made <em>stricter</em> here — which would
/// refuse messages the server accepts. Lengths count UTF-16 code units, which is what
/// <see cref="string.Length"/> and the server both count.
/// </remarks>
public static partial class FeedbackLimits
{
    /// <summary>Characters in the message, after trimming.</summary>
    public const int MessageMaxLength = 5000;

    /// <summary>Characters in the optional reply address.</summary>
    public const int EmailMaxLength = 254;

    /// <summary>Characters in <c>appVersion</c>.</summary>
    public const int AppVersionMaxLength = 64;

    /// <summary>Characters in <c>os</c>.</summary>
    public const int OperatingSystemMaxLength = 128;

    /// <summary>
    /// True when <paramref name="email"/> is blank (not given) or looks like
    /// <c>local@domain.tld</c> the way the server checks it.
    /// </summary>
    public static bool IsAcceptableEmail(string? email)
    {
        var trimmed = email?.Trim() ?? "";
        return trimmed.Length == 0
            || (trimmed.Length <= EmailMaxLength && EmailPattern().IsMatch(trimmed));
    }

    /// <summary>
    /// The message as the server will store it: line endings as <c>\n</c>, invisible
    /// control characters other than tab and newline removed, and trimmed.
    /// </summary>
    /// <remarks>
    /// The server rejects a message that contains a control character, and pasted text
    /// sometimes carries one the user cannot see (a form feed, a stray <c>\0</c>).
    /// Removing it here keeps a paste from turning into a "something went wrong".
    /// </remarks>
    public static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "";

        var builder = new StringBuilder(message.Length);
        for (var i = 0; i < message.Length; i++)
        {
            var c = message[i];
            if (c == '\r')
            {
                builder.Append('\n');
                if (i + 1 < message.Length && message[i + 1] == '\n')
                    i++;
                continue;
            }

            if (c is '\n' or '\t' || !IsControl(c))
                builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>True when the normalized message is non-empty and within the limit.</summary>
    public static bool IsSendableMessage(string? message)
    {
        var normalized = NormalizeMessage(message);
        return normalized.Length is > 0 and <= MessageMaxLength;
    }

    /// <summary>
    /// <paramref name="version"/> reduced to the API's alphabet (<c>0-9 A-Z a-z . + -</c>)
    /// and length, or <c>unknown</c> when nothing is left.
    /// </summary>
    public static string NormalizeAppVersion(string? version)
    {
        var kept = new string((version ?? "").Where(IsVersionChar).ToArray());
        if (kept.Length > AppVersionMaxLength)
            kept = kept[..AppVersionMaxLength];

        return kept.Length == 0 ? "unknown" : kept;
    }

    /// <summary>
    /// <paramref name="description"/> as one line without control characters, within the
    /// API's length, or <c>unknown</c> when nothing is left.
    /// </summary>
    public static string NormalizeOperatingSystem(string? description)
    {
        var builder = new StringBuilder();
        foreach (var c in description ?? "")
            builder.Append(IsControl(c) ? ' ' : c);

        var line = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (line.Length > OperatingSystemMaxLength)
            line = line[..OperatingSystemMaxLength].TrimEnd();

        return line.Length == 0 ? "unknown" : line;
    }

    // C0 and C1 controls, as the server's CONTROL pattern defines them.
    private static bool IsControl(char c) => c <= '\u001F' || c is >= '\u007F' and <= '\u009F';

    private static bool IsVersionChar(char c) =>
        c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '.' or '+' or '-';

    // The server's EMAIL pattern, character for character.
    [GeneratedRegex("""^[^\s@<>()\[\]",;:\\]+@[^\s@<>()\[\]",;:\\]+\.[^\s@<>()\[\]",;:\\]+$""", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
