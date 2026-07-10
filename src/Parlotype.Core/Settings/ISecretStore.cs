namespace Parlotype.Core.Settings;

/// <summary>
/// Stores small secrets (API keys) outside settings.json, encrypted at rest
/// where the OS supports it. Backs the bring-your-own-key cloud speech
/// providers (ADR-032) — a secret never appears in the plaintext settings
/// file, and is never logged.
/// </summary>
public interface ISecretStore
{
    /// <summary>Returns the stored value for <paramref name="key"/>, or null when absent or undecryptable.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Null or empty value removes the secret.</summary>
    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}
