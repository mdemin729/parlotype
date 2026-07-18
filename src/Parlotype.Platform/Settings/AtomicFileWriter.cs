namespace Parlotype.Platform.Settings;

/// <summary>
/// Write-to-temp-then-move so a crash or power loss mid-write can never leave
/// a truncated file behind (security audit 2026-07-11, S7). Both JSON stores
/// silently fall back to an empty dictionary on a corrupt file, which would
/// otherwise cost the user their settings or stored API keys without notice.
/// Same pattern as the model downloaders' <c>.tmp</c> + move.
/// </summary>
internal static class AtomicFileWriter
{
    public static async Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, contents, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}
