using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// In-memory <see cref="IPromptTemplateRegistry"/> for UI tests. Mirrors the
/// real implementation's semantics: a non-deletable built-in default is always
/// present, edits/removals of built-ins throw, and active selection falls back
/// to the built-in.
/// </summary>
public sealed class MockPromptTemplateRegistry : IPromptTemplateRegistry
{
    public const string BuiltInId = "builtin-default";

    public static readonly PromptTemplate BuiltIn = new(
        BuiltInId, "Default (verbatim transcription)", "Transcribe in {language}.", IsBuiltIn: true);

    private readonly List<PromptTemplate> _users = [];
    private string _activeId = BuiltInId;

    public Task<IReadOnlyList<PromptTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<PromptTemplate> { BuiltIn };
        all.AddRange(_users);
        return Task.FromResult<IReadOnlyList<PromptTemplate>>(all);
    }

    public Task<PromptTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id == BuiltInId)
            return Task.FromResult<PromptTemplate?>(BuiltIn);
        return Task.FromResult(_users.FirstOrDefault(p => p.Id == id));
    }

    public Task AddOrUpdateAsync(PromptTemplate prompt, CancellationToken cancellationToken = default)
    {
        if (prompt.IsBuiltIn || prompt.Id == BuiltInId)
            throw new InvalidOperationException("Built-in prompts cannot be modified.");

        var index = _users.FindIndex(p => p.Id == prompt.Id);
        if (index >= 0)
            _users[index] = prompt;
        else
            _users.Add(prompt);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id == BuiltInId)
            throw new InvalidOperationException("The built-in prompt cannot be deleted.");

        _users.RemoveAll(p => p.Id == id);
        if (_activeId == id)
            _activeId = BuiltInId;
        return Task.CompletedTask;
    }

    public Task<PromptTemplate> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var match = _activeId == BuiltInId
            ? BuiltIn
            : _users.FirstOrDefault(p => p.Id == _activeId) ?? BuiltIn;
        return Task.FromResult(match);
    }

    public Task SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
        _activeId = id;
        return Task.CompletedTask;
    }
}
