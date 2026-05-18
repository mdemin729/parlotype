using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech.LlamaServer;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform.Speech.LlamaServer;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Covers the managed-install surface added in Phase 7:
/// Installed list, Available list, install/uninstall, set-active for managed
/// and manual, and the manual-mode visibility used by the badge.
/// </summary>
public sealed class LlamaCppSettingsViewModelManagedTests : IDisposable
{
    private readonly string _root;
    private readonly MockSettingsService _settings = new();
    private readonly JsonLlamaServerRegistry _registry;
    private readonly MockLlamaServerCatalog _catalog = new();
    private readonly MockLlamaServerInstaller _installer;

    public LlamaCppSettingsViewModelManagedTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _registry = new JsonLlamaServerRegistry(
            _root, _settings, NullLogger<JsonLlamaServerRegistry>.Instance);
        _installer = new MockLlamaServerInstaller(_registry);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private LlamaCppSettingsViewModel NewVm() => new(
        settings: _settings,
        recognizer: null,
        registry: _registry,
        catalog: _catalog,
        installer: _installer);

    private static LlamaServerVariant SampleVariant(
        string build = "b9198",
        LlamaServerBackend backend = LlamaServerBackend.Vulkan) => new(
            Build: build,
            Backend: backend,
            Os: LlamaServerOs.Windows,
            Arch: LlamaServerArch.X64,
            AssetName: $"llama-{build}-bin-win-vulkan-x64.zip",
            Bytes: 31_000_000,
            DownloadUrl: "https://example.test/v.zip",
            Sha256: null,
            CompanionAssetName: null,
            CompanionDownloadUrl: null,
            CompanionBytes: null,
            CompanionSha256: null);

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(20);
    }

    [AvaloniaFact]
    public void HasManagedFeatures_IsTrue_WhenAllDepsProvided()
    {
        var vm = NewVm();
        Assert.True(vm.HasManagedFeatures);
    }

    [AvaloniaFact]
    public void HasManagedFeatures_IsFalse_WhenOnlySettingsProvided()
    {
        var vm = new LlamaCppSettingsViewModel(_settings);
        Assert.False(vm.HasManagedFeatures);
    }

    [AvaloniaFact]
    public async Task Initialize_PopulatesAvailableFromCatalog()
    {
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { SampleVariant() }));
        var vm = NewVm();

        await WaitForAsync(() => vm.Available.Count > 0);

        var only = Assert.Single(vm.Available);
        Assert.Equal("b9198", only.Build);
        Assert.Equal(LlamaServerBackend.Vulkan, only.Backend);
        Assert.False(only.IsAlreadyInstalled);
    }

    [AvaloniaFact]
    public async Task InstallVariant_AddsToInstalledList_AndMarksAvailableAsInstalled()
    {
        var variant = SampleVariant();
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);

        var row = vm.Available.Single();
        await vm.InstallVariantCommand.ExecuteAsync(row);

        Assert.Single(_installer.InstalledVariants);
        Assert.Single(vm.Installed);
        Assert.True(vm.Available.Single().IsAlreadyInstalled);
    }

    [AvaloniaFact]
    public async Task UninstallInstall_RemovesFromList_AndClearsInstalledBadge()
    {
        var variant = SampleVariant();
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);

        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());
        Assert.Single(vm.Installed);

        var installedRow = vm.Installed.Single();
        await vm.UninstallInstallCommand.ExecuteAsync(installedRow);

        Assert.Contains(installedRow.Id, _installer.UninstalledIds);
        Assert.Empty(vm.Installed);
        Assert.False(vm.Available.Single().IsAlreadyInstalled);
    }

    [AvaloniaFact]
    public async Task SetActiveManaged_MarksRowActive_AndUpdatesActiveProperties()
    {
        var variant = SampleVariant();
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());

        var row = vm.Installed.Single();
        await vm.SetActiveManagedCommand.ExecuteAsync(row);

        Assert.True(vm.IsManagedActive);
        Assert.False(vm.IsManualActive);
        Assert.NotNull(vm.ActiveManagedInstall);
        Assert.Equal(row.Id, vm.ActiveManagedInstall!.Id);
        Assert.True(vm.Installed.Single().IsActive);
    }

    [AvaloniaFact]
    public async Task SetActiveManual_FlipsManualActive_AndClearsManagedActive()
    {
        // Manual mode resolves through LlamaCppServerFolder, so the test must
        // mirror real usage by pointing at one before flipping the selector.
        var ct = TestContext.Current.CancellationToken;
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, @"C:\my-llama", ct);

        var variant = SampleVariant();
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());
        await vm.SetActiveManagedCommand.ExecuteAsync(vm.Installed.Single());
        Assert.True(vm.IsManagedActive);

        await vm.SetActiveManualCommand.ExecuteAsync(null);

        Assert.True(vm.IsManualActive);
        Assert.False(vm.IsManagedActive);
        Assert.Null(vm.ActiveManagedInstall);
    }

    [AvaloniaFact]
    public async Task CheckForUpdates_SetsBannerWhenLatestBuildExceedsActiveBuild()
    {
        var older = SampleVariant(build: "b9000");
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9000", new[] { older }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());
        await vm.SetActiveManagedCommand.ExecuteAsync(vm.Installed.Single());

        var newer = SampleVariant(build: "b9198");
        _catalog.SetGroups(
            new LlamaServerReleaseGroup("b9198", new[] { newer }),
            new LlamaServerReleaseGroup("b9000", new[] { older }));

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(_catalog.ForceRefreshCount > 0);
        Assert.True(vm.IsUpdateAvailable);
        Assert.Equal("b9198", vm.LatestAvailableBuild);
    }

    [AvaloniaFact]
    public async Task CheckForUpdates_NoBanner_WhenActiveMatchesLatest()
    {
        var variant = SampleVariant(build: "b9198");
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());
        await vm.SetActiveManagedCommand.ExecuteAsync(vm.Installed.Single());

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.False(vm.IsUpdateAvailable);
        Assert.Equal("b9198", vm.LatestAvailableBuild);
    }

    [AvaloniaFact]
    public async Task InstallVariant_FailingInstaller_SurfacesErrorMessage()
    {
        var variant = SampleVariant();
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9198", new[] { variant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);

        _installer.InstallError = new InvalidOperationException("boom");
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());

        Assert.Equal("boom", vm.CatalogErrorMessage);
        Assert.Empty(vm.Installed);
    }

    [AvaloniaFact]
    public async Task CatalogFetchFailure_SurfacesErrorMessage()
    {
        _catalog.FetchError = new HttpRequestException("offline");
        var vm = NewVm();
        await WaitForAsync(() => vm.CatalogErrorMessage is not null);

        Assert.Equal("offline", vm.CatalogErrorMessage);
        Assert.Empty(vm.Available);
    }

    [AvaloniaFact]
    public async Task ManualBadge_IsVisible_WhenActiveSelectorIsManual()
    {
        var ct = TestContext.Current.CancellationToken;
        await _settings.SetAsync(SettingsKeys.LlamaCppActiveInstall, "manual", ct);
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, @"C:\my-llama", ct);

        var vm = NewVm();
        // RefreshInstalledCommand reloads + computes IsManualActive
        await vm.RefreshInstalledCommand.ExecuteAsync(null);

        Assert.True(vm.IsManualActive);
        Assert.False(vm.IsManagedActive);
    }

    [AvaloniaFact]
    public async Task ExistingTests_OneArgConstructor_StillWorks()
    {
        // Backward-compat: the single-arg constructor used by existing tests
        // must not require the new dependencies.
        var vm = new LlamaCppSettingsViewModel(_settings);
        Assert.False(vm.HasManagedFeatures);
        Assert.Empty(vm.Available);
        Assert.Empty(vm.Installed);
        // Commands should be no-ops, not crash.
        await vm.InstallVariantCommand.ExecuteAsync(null);
        await vm.UninstallInstallCommand.ExecuteAsync(null);
        await vm.SetActiveManagedCommand.ExecuteAsync(null);
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
    }
}
