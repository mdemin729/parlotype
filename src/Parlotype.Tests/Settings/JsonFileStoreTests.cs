using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Platform.Settings;
using Xunit;

namespace Parlotype.Tests.Settings;

/// <summary>
/// Exercises the JSON key-value persistence shared by <c>JsonSettingsService</c>
/// and <c>JsonWindowStateService</c> (ADR-040), using a temp-file-backed subclass
/// so the tests never touch the real user's local app data.
/// </summary>
public class JsonFileStoreTests
{
    private sealed class TestFileStore : JsonFileStore
    {
        public TestFileStore(string path) : base(path, NullLogger.Instance)
        {
        }

        public Task<T?> GetAsync<T>(string key) => GetCoreAsync<T>(key, default);
        public Task SetAsync<T>(string key, T value) => SetCoreAsync(key, value, default);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"parlotype-test-{Guid.NewGuid()}.json");

    [Fact]
    public async Task SetThenGet_RoundTripsValue()
    {
        var path = TempPath();
        try
        {
            var store = new TestFileStore(path);
            await store.SetAsync("greeting", "hello");

            Assert.Equal("hello", await store.GetAsync<string>("greeting"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsDefault()
    {
        var path = TempPath();
        try
        {
            var store = new TestFileStore(path);

            Assert.Null(await store.GetAsync<string>("does-not-exist"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TwoStores_WithDifferentPaths_DoNotShareState()
    {
        var pathA = TempPath();
        var pathB = TempPath();
        try
        {
            var storeA = new TestFileStore(pathA);
            var storeB = new TestFileStore(pathB);

            await storeA.SetAsync("key", "from-a");

            Assert.Equal("from-a", await storeA.GetAsync<string>("key"));
            Assert.Null(await storeB.GetAsync<string>("key"));
            Assert.True(File.Exists(pathA));
            Assert.False(File.Exists(pathB));
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }

    [Fact]
    public async Task SetThenGet_RoundTripsStructValue()
    {
        var path = TempPath();
        try
        {
            var store = new TestFileStore(path);
            await store.SetAsync("position", new WindowPositionForTest(300, 200));

            var result = await store.GetAsync<WindowPositionForTest?>("position");

            Assert.Equal(new WindowPositionForTest(300, 200), result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private readonly record struct WindowPositionForTest(int X, int Y);
}
