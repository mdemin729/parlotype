using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Settings;

/// <summary>
/// <see cref="ISecretStore"/> backed by a small JSON file living alongside
/// settings.json. Values are encrypted at rest with Windows DPAPI
/// (<see cref="DataProtectionScope.CurrentUser"/>) when running on Windows;
/// on other platforms values are stored as base64-encoded plaintext with a
/// one-time warning that OS-level encryption is unavailable.
/// </summary>
/// <remarks>
/// Deliberately not built on <see cref="JsonFileStore"/>: that base class has
/// no per-key delete operation (needed for "null/empty removes the secret")
/// and its generic <c>T</c> value serialization has no hook for the
/// protect/unprotect step, so a small standalone implementation is clearer
/// than bending the shared base class to fit. Mirrors
/// <see cref="JsonSettingsService"/>'s path resolution and
/// <see cref="SemaphoreSlim"/> thread-safety pattern.
/// </remarks>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "secrets.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DpapiSecretStore> _logger;
    private bool _warnedNonWindows;

    public DpapiSecretStore(ILogger<DpapiSecretStore> logger) : this(DefaultPath, logger)
    {
    }

    /// <summary>Testability seam: points the store at a temp file instead of the real user data folder.</summary>
    internal DpapiSecretStore(string path, ILogger<DpapiSecretStore> logger)
    {
        _path = path;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            if (!dict.TryGetValue(key, out var stored) || string.IsNullOrEmpty(stored))
                return null;

            return Unprotect(key, stored);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);

            if (string.IsNullOrEmpty(value))
                dict.Remove(key);
            else
                dict[key] = Protect(value);

            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string Protect(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);

        if (OperatingSystem.IsWindows())
        {
            var cipherBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }

        WarnNonWindowsOnce();
        return Convert.ToBase64String(plainBytes);
    }

    /// <summary>
    /// Reverses <see cref="Protect"/>. Never throws — a value that fails to
    /// decrypt (e.g. copied from another machine or user account, or written
    /// by a different encryption scope) is logged and treated as absent so
    /// callers fall back to the "no key configured" path instead of crashing.
    /// </summary>
    private string? Unprotect(string key, string stored)
    {
        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromBase64String(stored);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Secret '{Key}' is not valid base64 — treating as absent", key);
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            WarnNonWindowsOnce();
            return Encoding.UTF8.GetString(rawBytes);
        }

        try
        {
            var plainBytes = ProtectedData.Unprotect(rawBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex,
                "Failed to decrypt secret '{Key}' — it may have been copied from another machine or " +
                "user account. Treating as absent; re-enter it in Settings.", key);
            return null;
        }
    }

    private void WarnNonWindowsOnce()
    {
        if (_warnedNonWindows)
            return;

        _warnedNonWindows = true;
        _logger.LogWarning(
            "OS-level secret encryption (DPAPI) is only available on Windows; secrets are stored " +
            "base64-encoded, not encrypted, on this platform.");
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return new Dictionary<string, string>();

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load secrets file: {Path}", _path);
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveAsync(Dictionary<string, string> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await File.WriteAllTextAsync(_path, json, cancellationToken);
    }
}
