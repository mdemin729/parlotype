namespace Parlotype.Core.Settings;

/// <summary>
/// Knows where the Velopack pack folder is, so the app can recognise
/// <em>user-supplied</em> paths that fall inside it.
/// </summary>
/// <remarks>
/// <para>
/// Velopack installs to <c>%LOCALAPPDATA%\{packId}</c>, replaces <c>current\</c> on
/// every update, and deletes the <em>entire</em> pack folder on uninstall and on a
/// re-run of <c>Setup.exe</c> (ADR-053). <see cref="IAppPaths"/> guarantees that
/// nothing Parlotype writes ever lands there.
/// </para>
/// <para>
/// It cannot make that guarantee for paths the user types or browses to. The manual
/// <c>llama-server</c> folder is the one such path in the app, and its placeholder
/// pointed at <c>%LOCALAPPDATA%\parlotype\llama-server</c> — the pack folder itself,
/// Windows paths being case-insensitive — until ADR-065. Such a path is
/// <em>detected and reported</em>, never rewritten: the binaries are the user's, and
/// silently repointing a working install is the half-completed migration ADR-053
/// already declined to ship.
/// </para>
/// </remarks>
public static class VelopackPackFolder
{
    /// <summary>
    /// The pack id passed to <c>vpk pack</c>. Permanent — changing it would orphan
    /// every existing install (ADR-053).
    /// </summary>
    public const string PackId = "Parlotype";

    /// <summary>
    /// Absolute path of the Velopack pack folder, or <c>null</c> on macOS and Linux,
    /// which have no Velopack install root yet.
    /// </summary>
    public static string? Root =>
        OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                PackId)
            : null;

    /// <summary>
    /// True when <paramref name="path"/> is the pack folder or sits underneath it.
    /// </summary>
    /// <remarks>
    /// The comparison is case-insensitive on purpose: that case-folding is exactly
    /// what made <c>%LOCALAPPDATA%\parlotype</c> and the <c>Parlotype</c> pack folder
    /// the same directory. The separator is part of the prefix so that the sibling
    /// data root <c>parlotype-data</c> — which shares the pack folder's first nine
    /// characters — does not match.
    /// </remarks>
    public static bool Contains(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (Root is not { } root)
            return false;

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (
            ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Unparseable input is not a path we can prove anything about, and this
            // runs on every keystroke in the folder box — it must not throw.
            return false;
        }

        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
