using Parlotype.Core.Settings;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The once-only auto-show gate (ADR-055): unset/garbage flag → show once and
/// stamp <c>"True"</c> first; anything already stamped → never again; a
/// failing settings store must not take app startup down with it.
/// </summary>
public class OnboardingServiceTests
{
    private sealed class SpyOnboardingService(ISettingsService settings)
        : OnboardingService(new NullServiceProvider(), settings)
    {
        public int ShowWizardCoreCount { get; private set; }

        protected override void ShowWizardCore() => ShowWizardCoreCount++;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task FirstRun_ShowsWizard_AndStampsTheFlagFirst()
    {
        var settings = new MockSettingsService();
        var service = new SpyOnboardingService(settings);

        await service.MaybeShowOnFirstRunAsync();

        Assert.Equal(1, service.ShowWizardCoreCount);
        Assert.Equal("True", await settings.GetAsync<string>(SettingsKeys.OnboardingCompleted, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AlreadyOffered_DoesNotShowAgain()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.OnboardingCompleted, "True", TestContext.Current.CancellationToken);
        var service = new SpyOnboardingService(settings);

        await service.MaybeShowOnFirstRunAsync();

        Assert.Equal(0, service.ShowWizardCoreCount);
    }

    [Fact]
    public async Task UnparsableFlag_CountsAsNeverOffered()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.OnboardingCompleted, "banana", TestContext.Current.CancellationToken);
        var service = new SpyOnboardingService(settings);

        await service.MaybeShowOnFirstRunAsync();

        Assert.Equal(1, service.ShowWizardCoreCount);
    }

    [Fact]
    public async Task SecondCall_AfterFirstShow_IsANoOp()
    {
        var settings = new MockSettingsService();
        var service = new SpyOnboardingService(settings);

        await service.MaybeShowOnFirstRunAsync();
        await service.MaybeShowOnFirstRunAsync();

        Assert.Equal(1, service.ShowWizardCoreCount);
    }

    [Fact]
    public async Task FailingSettingsWrite_IsSwallowed_AndSkipsTheTour()
    {
        // If the flag cannot be persisted, showing anyway would mean showing on
        // every launch — worse than not showing at all. The write happens
        // before the show, so the throw skips the tour and must not propagate
        // into app startup.
        var settings = new MockSettingsService();
        settings.ThrowWritesFor.Add(SettingsKeys.OnboardingCompleted);
        var service = new SpyOnboardingService(settings);

        await service.MaybeShowOnFirstRunAsync();

        Assert.Equal(0, service.ShowWizardCoreCount);
    }

    [Fact]
    public void ShowWizard_IsUnconditional()
    {
        var settings = new MockSettingsService();
        var service = new SpyOnboardingService(settings);

        service.ShowWizard();
        service.ShowWizard();

        Assert.Equal(2, service.ShowWizardCoreCount);
    }
}
