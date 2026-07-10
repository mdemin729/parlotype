using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Platform.Settings;
using Xunit;

namespace Parlotype.Tests.Settings;

/// <summary>
/// Exercises <see cref="DpapiSecretStore"/> against a temp file so tests never
/// touch the real user's local app data (mirrors <c>JsonFileStoreTests</c>).
/// Protection-specific behavior (actual DPAPI encrypt/decrypt round-trip) is
/// only meaningfully different on Windows; the get/set contract is exercised
/// on every platform since non-Windows falls back to base64 plaintext with
/// the same public API.
/// </summary>
public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _path;

    public DpapiSecretStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"parlotype-test-secrets-{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* best effort */ }
    }

    private DpapiSecretStore NewStore() => new(_path, NullLogger<DpapiSecretStore>.Instance);

    [Fact]
    public async Task SetThenGet_RoundTripsValue()
    {
        var store = NewStore();

        await store.SetAsync("api-key", "sk-abc123");

        Assert.Equal("sk-abc123", await store.GetAsync("api-key"));
    }

    [Fact]
    public async Task Get_UnknownKey_ReturnsNull()
    {
        var store = NewStore();

        Assert.Null(await store.GetAsync("does-not-exist"));
    }

    [Fact]
    public async Task Set_Overwrite_ReplacesValue()
    {
        var store = NewStore();

        await store.SetAsync("api-key", "first-value");
        await store.SetAsync("api-key", "second-value");

        Assert.Equal("second-value", await store.GetAsync("api-key"));
    }

    [Fact]
    public async Task Set_NullValue_RemovesSecret()
    {
        var store = NewStore();
        await store.SetAsync("api-key", "sk-abc123");

        await store.SetAsync("api-key", null);

        Assert.Null(await store.GetAsync("api-key"));
    }

    [Fact]
    public async Task Set_EmptyValue_RemovesSecret()
    {
        var store = NewStore();
        await store.SetAsync("api-key", "sk-abc123");

        await store.SetAsync("api-key", string.Empty);

        Assert.Null(await store.GetAsync("api-key"));
    }

    [Fact]
    public async Task RemovedSecret_IsNotPersistedInFile()
    {
        var store = NewStore();
        await store.SetAsync("api-key", "sk-abc123");
        await store.SetAsync("api-key", null);

        var json = await File.ReadAllTextAsync(_path);

        Assert.DoesNotContain("api-key", json);
    }

    [Fact]
    public async Task DifferentKeys_DoNotCollide()
    {
        var store = NewStore();

        await store.SetAsync("openai-key", "sk-openai");
        await store.SetAsync("xai-key", "sk-xai");

        Assert.Equal("sk-openai", await store.GetAsync("openai-key"));
        Assert.Equal("sk-xai", await store.GetAsync("xai-key"));
    }

    [Fact]
    public async Task TwoStores_WithDifferentPaths_DoNotShareState()
    {
        var pathB = Path.Combine(Path.GetTempPath(), $"parlotype-test-secrets-{Guid.NewGuid()}.json");
        try
        {
            var storeA = NewStore();
            var storeB = new DpapiSecretStore(pathB, NullLogger<DpapiSecretStore>.Instance);

            await storeA.SetAsync("api-key", "from-a");

            Assert.Equal("from-a", await storeA.GetAsync("api-key"));
            Assert.Null(await storeB.GetAsync("api-key"));
        }
        finally
        {
            try { File.Delete(pathB); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task StoredValue_IsNotPlaintextInFile()
    {
        // Regardless of platform (DPAPI ciphertext on Windows, base64 elsewhere),
        // the raw secret must never appear verbatim in the persisted file.
        var store = NewStore();
        await store.SetAsync("api-key", "sk-super-secret-value");

        var json = await File.ReadAllTextAsync(_path);

        Assert.DoesNotContain("sk-super-secret-value", json);
    }

    [Fact]
    public async Task Persists_AcrossStoreInstances()
    {
        var storeA = NewStore();
        await storeA.SetAsync("api-key", "sk-abc123");

        var storeB = NewStore();

        Assert.Equal("sk-abc123", await storeB.GetAsync("api-key"));
    }

    [Fact]
    public async Task Windows_Ciphertext_DoesNotRoundTripAcrossEntropyMismatch()
    {
        if (!OperatingSystem.IsWindows())
            return; // DPAPI-specific behavior only applies on Windows.

        // A value written by hand (not via ProtectedData.Protect) simulates a
        // secrets.json copied from another machine/user profile: Unprotect must
        // fail closed (return null) rather than throw.
        var corruptPath = Path.Combine(Path.GetTempPath(), $"parlotype-test-secrets-{Guid.NewGuid()}.json");
        try
        {
            await File.WriteAllTextAsync(corruptPath,
                """{"api-key":"dGhpcyBpcyBub3QgcmVhbCBjaXBoZXJ0ZXh0"}""");
            var store = new DpapiSecretStore(corruptPath, NullLogger<DpapiSecretStore>.Instance);

            var result = await store.GetAsync("api-key");

            Assert.Null(result);
        }
        finally
        {
            try { File.Delete(corruptPath); } catch { /* best effort */ }
        }
    }
}
