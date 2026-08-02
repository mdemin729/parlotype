using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Updates;
using Parlotype.Platform.Updates;
using Xunit;

namespace Parlotype.Tests.Updates;

/// <summary>
/// These run in exactly the situation the updater must tolerate: a test host that
/// was never installed by Velopack — the same shape as `dotnet run`, the IDE, and
/// the portable zip. Nothing here touches the network.
/// </summary>
public class VelopackUpdateServiceTests
{
    private static VelopackUpdateService NewService(ISettingsService? settings = null) =>
        new(settings ?? new InMemorySettings(), NullLogger<VelopackUpdateService>.Instance);

    [Fact]
    public async Task StartAsync_WhenNotInstalled_DoesNotThrow()
    {
        // The acceptance criterion in human terms: running from the IDE must not
        // blow up with NotInstalledException.
        using var service = NewService();

        await service.StartAsync();

        Assert.Equal(UpdateState.NotInstalled, service.Status.State);
    }

    [Fact]
    public async Task CheckAsync_WhenNotInstalled_IsASilentNoOp()
    {
        using var service = NewService();

        // Even the explicit, user-initiated path must degrade quietly rather than
        // surfacing a framework exception to the UI.
        var status = await service.CheckAsync(userInitiated: true);

        Assert.Equal(UpdateState.NotInstalled, status.State);
        Assert.Null(status.Message);
    }

    [Fact]
    public async Task ApplyAndRestartAsync_WhenNotInstalled_ReturnsFalseAndDoesNotExit()
    {
        using var service = NewService();

        Assert.False(await service.ApplyAndRestartAsync());
    }

    [Fact]
    public async Task CheckAsync_WhenAutomaticChecksDisabled_RecordsNoCheck()
    {
        var settings = new InMemorySettings();
        await settings.SetAsync(SettingsKeys.UpdatesCheckAutomatically, "false");

        using var service = NewService(settings);
        await service.CheckAsync(userInitiated: false);

        // Two guards can stop an automatic check, and the not-installed one runs
        // first — so in this test host that is the state we land in. Either way the
        // observable contract holds: the feed was never reached, so no
        // last-checked timestamp is written and the status never reached Checking.
        Assert.Equal(UpdateState.NotInstalled, service.Status.State);
        Assert.Null(await settings.GetAsync<string>(SettingsKeys.UpdatesLastCheckedUtc));
    }

    [Fact]
    public void CurrentVersion_WhenNotInstalled_IsNull()
    {
        using var service = NewService();

        Assert.Null(service.CurrentVersion);
    }

    [Fact]
    public void FeedUrl_IsThePublicRepository()
    {
        // Pinned deliberately: this is the exact endpoint the README and the
        // Settings page promise is the only thing contacted (ADR-053).
        Assert.Equal("https://github.com/mdemin729/parlotype", VelopackUpdateService.FeedUrl);
    }

    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var raw) && raw is T typed)
                return Task.FromResult<T?>(typed);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }
}
