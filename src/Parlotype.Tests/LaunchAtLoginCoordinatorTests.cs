using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Platform.Startup;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// The default-on policy and OS reconciliation for launch at sign-in (ADR-059).
/// </summary>
public class LaunchAtLoginCoordinatorTests
{
    private static LaunchAtLoginCoordinator Build(
        FakeLaunchAtLoginService launchAtLogin,
        InMemorySettingsService? settings = null) =>
        new(settings ?? new InMemorySettingsService(),
            launchAtLogin,
            NullLogger<LaunchAtLoginCoordinator>.Instance);

    [Fact]
    public async Task ReadPreference_WithNothingStored_DefaultsToOn()
    {
        var coordinator = Build(new FakeLaunchAtLoginService());

        Assert.True(await coordinator.ReadPreferenceAsync());
    }

    [Theory]
    [InlineData("not a bool")]
    [InlineData("")]
    public async Task ReadPreference_WithUnparsableValue_DefaultsToOn(string stored)
    {
        var settings = new InMemorySettingsService();
        await settings.SetAsync(SettingsKeys.LaunchAtLogin, stored);

        var coordinator = Build(new FakeLaunchAtLoginService(), settings);

        Assert.True(await coordinator.ReadPreferenceAsync());
    }

    [Fact]
    public async Task ReadPreference_HonoursAnExplicitOptOut()
    {
        var settings = new InMemorySettingsService();
        await settings.SetAsync(SettingsKeys.LaunchAtLogin, "False");

        var coordinator = Build(new FakeLaunchAtLoginService(), settings);

        Assert.False(await coordinator.ReadPreferenceAsync());
    }

    /// <summary>
    /// The upgrade path: an existing install has no stored preference, so the
    /// first launch after updating registers it (the decision recorded in
    /// ADR-059).
    /// </summary>
    [Fact]
    public async Task Reconcile_WithNoStoredPreference_Registers()
    {
        var launchAtLogin = new FakeLaunchAtLoginService();
        var coordinator = Build(launchAtLogin);

        var state = await coordinator.ReconcileAsync();

        Assert.Equal(LaunchAtLoginState.Enabled, state);
        Assert.True(launchAtLogin.IsRegistered);
    }

    [Fact]
    public async Task Reconcile_WithOptOut_UnregistersAnExistingEntry()
    {
        var settings = new InMemorySettingsService();
        await settings.SetAsync(SettingsKeys.LaunchAtLogin, "False");
        var launchAtLogin = new FakeLaunchAtLoginService(registered: true);

        var state = await Build(launchAtLogin, settings).ReconcileAsync();

        Assert.Equal(LaunchAtLoginState.Disabled, state);
        Assert.False(launchAtLogin.IsRegistered);
    }

    [Fact]
    public async Task Reconcile_WhenAlreadyInSync_DoesNotTouchTheRegistration()
    {
        var launchAtLogin = new FakeLaunchAtLoginService(registered: true);

        await Build(launchAtLogin).ReconcileAsync();

        Assert.Equal(0, launchAtLogin.SetCount);
    }

    /// <summary>
    /// A stale entry (an old install location) reads as Disabled, so the
    /// rewrite happens through the ordinary reconcile path.
    /// </summary>
    [Fact]
    public async Task Reconcile_WithAStaleRegistration_RewritesIt()
    {
        var launchAtLogin = new FakeLaunchAtLoginService(registered: false);

        await Build(launchAtLogin).ReconcileAsync();

        Assert.Equal(1, launchAtLogin.SetCount);
        Assert.True(launchAtLogin.IsRegistered);
    }

    /// <summary>
    /// The user's veto in Task Manager is a deliberate decision. Reconciliation
    /// must not paper over it, and must not rewrite the stored preference.
    /// </summary>
    [Fact]
    public async Task Reconcile_WhenWindowsHasVetoedTheEntry_ChangesNothing()
    {
        var launchAtLogin = new FakeLaunchAtLoginService(registered: true)
        {
            ForcedState = LaunchAtLoginState.BlockedByOperatingSystem,
        };

        var state = await Build(launchAtLogin).ReconcileAsync();

        Assert.Equal(LaunchAtLoginState.BlockedByOperatingSystem, state);
        Assert.Equal(0, launchAtLogin.SetCount);
    }

    [Fact]
    public async Task Reconcile_OnAnUnsupportedBuild_DoesNothing()
    {
        var launchAtLogin = new FakeLaunchAtLoginService(isSupported: false);

        var state = await Build(launchAtLogin).ReconcileAsync();

        Assert.Equal(LaunchAtLoginState.Unsupported, state);
        Assert.Equal(0, launchAtLogin.SetCount);
    }

    [Fact]
    public async Task SetEnabled_PersistsThePreferenceAndAppliesIt()
    {
        var settings = new InMemorySettingsService();
        var launchAtLogin = new FakeLaunchAtLoginService(registered: true);

        var state = await Build(launchAtLogin, settings)
            .SetEnabledAsync(false);

        Assert.Equal(LaunchAtLoginState.Disabled, state);
        Assert.False(launchAtLogin.IsRegistered);
        Assert.Equal(
            "False",
            await settings.GetAsync<string>(SettingsKeys.LaunchAtLogin));
    }

    /// <summary>
    /// A registry that will not cooperate is a lost convenience, never an
    /// exception escaping into startup or the Settings page.
    /// </summary>
    [Fact]
    public async Task Reconcile_WhenTheServiceThrows_ReportsDisabledInsteadOfPropagating()
    {
        var launchAtLogin = new FakeLaunchAtLoginService { ThrowOnGetState = true };

        var state = await Build(launchAtLogin).ReconcileAsync();

        Assert.Equal(LaunchAtLoginState.Disabled, state);
    }

    private sealed class FakeLaunchAtLoginService : ILaunchAtLoginService
    {
        public FakeLaunchAtLoginService(bool isSupported = true, bool registered = false)
        {
            IsSupported = isSupported;
            IsRegistered = registered;
        }

        public bool IsSupported { get; }
        public bool IsRegistered { get; private set; }
        public int SetCount { get; private set; }
        public LaunchAtLoginState? ForcedState { get; init; }
        public bool ThrowOnGetState { get; init; }

        public LaunchAtLoginState GetState()
        {
            if (ThrowOnGetState)
                throw new InvalidOperationException("registry unavailable");
            if (!IsSupported)
                return LaunchAtLoginState.Unsupported;

            return ForcedState
                ?? (IsRegistered ? LaunchAtLoginState.Enabled : LaunchAtLoginState.Disabled);
        }

        public bool SetEnabled(bool enabled)
        {
            SetCount++;
            if (!IsSupported)
                return false;

            IsRegistered = enabled;
            return true;
        }
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        private readonly Dictionary<string, object?> _values = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? (T?)value : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
