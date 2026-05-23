namespace Parlotype.Core.Speech;

/// <summary>
/// Persists the user's set of <see cref="PromptTemplate"/>s for the Gemma 4
/// engine and tracks which one is active. Implementations back the user-defined
/// prompts with <c>prompts.json</c> and always surface at least one
/// non-deletable built-in default, so a working prompt is guaranteed to exist.
/// </summary>
public interface IPromptTemplateRegistry
{
    /// <summary>Returns all prompts, built-ins first.</summary>
    Task<IReadOnlyList<PromptTemplate>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a specific prompt by id, or null if missing.</summary>
    Task<PromptTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new prompt or updates an existing user prompt. Throws
    /// <see cref="InvalidOperationException"/> if the id refers to a built-in.
    /// </summary>
    Task AddOrUpdateAsync(PromptTemplate prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user prompt. No-op if absent. Throws
    /// <see cref="InvalidOperationException"/> for built-ins. If the removed
    /// prompt was active, the active selection falls back to the built-in
    /// default.
    /// </summary>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active prompt, falling back to the built-in default when no
    /// selection exists or the selected prompt is missing.
    /// </summary>
    Task<PromptTemplate> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the active prompt by id.</summary>
    Task SetActiveAsync(string id, CancellationToken cancellationToken = default);
}
