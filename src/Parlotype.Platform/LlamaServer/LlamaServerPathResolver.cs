using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// Resolves which <c>llama-server</c> executable a consumer should launch. One
/// place for the rule so the speech recognizer and speech cleanup always start
/// the same build.
/// </summary>
internal static class LlamaServerPathResolver
{
    public const string ExecutableName = "llama-server.exe";

    /// <summary>
    /// Returns the absolute path to the executable, or <c>null</c> when no server
    /// is configured at all. A managed active install wins; manual mode, a missing
    /// selector, or a stale managed id falls through to
    /// <see cref="SettingsKeys.LlamaCppServerFolder"/>. There is no default folder
    /// (ADR-066). The file is not checked for existence.
    /// </summary>
    public static async Task<string?> ResolveExecutableAsync(
        ILlamaServerRegistry registry,
        ISettingsService settings,
        CancellationToken cancellationToken)
    {
        var active = await registry.GetActiveAsync(cancellationToken);
        if (active is { Source: LlamaServerSource.Managed })
            return Path.Combine(active.AbsolutePath, ExecutableName);

        var folder = await settings.GetAsync<string>(SettingsKeys.LlamaCppServerFolder, cancellationToken);
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : Path.Combine(folder.Trim(), ExecutableName);
    }
}
