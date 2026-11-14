using Parlotype.Core.LlamaServer;

namespace Parlotype.Platform.LlamaServer;

/// <summary>
/// Stops every <c>llama-server</c> process the app has started. The installer
/// sees one <see cref="ILlamaCppServerLifecycle"/>, but the executable it is
/// about to replace can be held open by more than one consumer: the Gemma 4
/// recognizer and speech cleanup each run their own process (ADR-073).
/// </summary>
public sealed class CompositeLlamaServerLifecycle : ILlamaCppServerLifecycle
{
    private readonly IReadOnlyList<ILlamaCppServerLifecycle> _members;

    public CompositeLlamaServerLifecycle(params ILlamaCppServerLifecycle[] members) =>
        _members = members;

    /// <summary>The consumers this stops, in order. Exposed for the DI composition test.</summary>
    public IReadOnlyList<ILlamaCppServerLifecycle> Members => _members;

    public async Task StopForReplacementAsync(CancellationToken cancellationToken = default)
    {
        foreach (var member in _members)
            await member.StopForReplacementAsync(cancellationToken);
    }
}
