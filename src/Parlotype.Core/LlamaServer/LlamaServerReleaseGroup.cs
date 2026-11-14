namespace Parlotype.Core.LlamaServer;

/// <summary>One llama.cpp build (e.g. <c>b9198</c>) with its variants.</summary>
/// <param name="Build">The build tag, <c>b</c> followed by the commit count.</param>
/// <param name="Variants">The archives of that build that can be installed here.</param>
/// <param name="ReleaseVersion">
/// The version of upstream's newest versioned release (e.g. <c>v1.0.0</c>) when
/// that release names this build; null for every other build (ADR-074). At most
/// one group in a catalog result carries it.
/// </param>
public sealed record LlamaServerReleaseGroup(
    string Build,
    IReadOnlyList<LlamaServerVariant> Variants,
    string? ReleaseVersion = null)
{
    /// <summary>
    /// Reads the number out of a build tag: 12359 for <c>b12359</c>. Returns
    /// false for anything that is not <c>b</c> followed only by digits, so a
    /// tag such as <c>b9195-rc1</c> has no number.
    /// </summary>
    public static bool TryParseBuildNumber(string? build, out int number)
    {
        number = 0;
        if (build is not { Length: > 1 } || build[0] != 'b')
            return false;

        var digits = build.AsSpan(1);
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        return int.TryParse(digits, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out number);
    }
}
