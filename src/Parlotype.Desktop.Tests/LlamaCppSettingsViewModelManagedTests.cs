using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform.LlamaServer;
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
    public async Task CheckForUpdates_NoBanner_WhenLatestInstalledOnDifferentBackend()
    {
        // Active install: older build on CUDA.
        // Also installed: latest build on a different backend (CPU).
        // Expected: banner hides because the user already has the latest, regardless of backend.
        var oldCuda = SampleVariant(build: "b9000", backend: LlamaServerBackend.Cuda13);
        var newCpu = SampleVariant(build: "b9198", backend: LlamaServerBackend.Cpu);
        _catalog.SetGroups(
            new LlamaServerReleaseGroup("b9198", new[] { newCpu }),
            new LlamaServerReleaseGroup("b9000", new[] { oldCuda }));

        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count >= 2);

        var cudaRow = vm.Available.Single(r => r.Variant.Backend == LlamaServerBackend.Cuda13);
        var cpuRow = vm.Available.Single(r => r.Variant.Backend == LlamaServerBackend.Cpu);
        await vm.InstallVariantCommand.ExecuteAsync(cudaRow);
        await vm.InstallVariantCommand.ExecuteAsync(cpuRow);

        var cudaInstall = vm.Installed.Single(i => i.Backend == LlamaServerBackend.Cuda13);
        await vm.SetActiveManagedCommand.ExecuteAsync(cudaInstall);

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("b9198", vm.LatestAvailableBuild);
        Assert.Equal("b9000", vm.ActiveManagedInstall?.Build);
        Assert.False(vm.IsUpdateAvailable);
    }

    [AvaloniaFact]
    public async Task InstallingLatestVariant_ClearsBanner_EvenWhenOlderStillActive()
    {
        // Older build installed and active → banner ON after checking for a newer build.
        // Install the newer build (on a different backend) without changing active.
        // Expected: banner turns OFF — Installed list now contains the latest.
        var older = SampleVariant(build: "b9000", backend: LlamaServerBackend.Cuda13);
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9000", new[] { older }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());
        await vm.SetActiveManagedCommand.ExecuteAsync(vm.Installed.Single());

        var newer = SampleVariant(build: "b9198", backend: LlamaServerBackend.Cpu);
        _catalog.SetGroups(
            new LlamaServerReleaseGroup("b9198", new[] { newer }),
            new LlamaServerReleaseGroup("b9000", new[] { older }));
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        Assert.True(vm.IsUpdateAvailable);

        var newerRow = vm.Available.Single(r => r.Variant.Build == "b9198");
        await vm.InstallVariantCommand.ExecuteAsync(newerRow);

        Assert.Equal("b9000", vm.ActiveManagedInstall?.Build);
        Assert.False(vm.IsUpdateAvailable);
    }

    [AvaloniaFact]
    public async Task RunningManagedInstall_TracksProbedBuild_NotRadioSelection()
    {
        // Two installs at different builds. The live server's /props reports the older
        // build, but the user clicks the radio to mark the newer one as active.
        // RunningManagedInstall must reflect what's running, not the radio choice.
        var older = SampleVariant(build: "b9204", backend: LlamaServerBackend.Cuda13);
        var newer = SampleVariant(build: "b9221", backend: LlamaServerBackend.Cpu);
        _catalog.SetGroups(
            new LlamaServerReleaseGroup("b9221", new[] { newer }),
            new LlamaServerReleaseGroup("b9204", new[] { older }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count >= 2);

        var olderRow = vm.Available.Single(r => r.Variant.Build == "b9204");
        var newerRow = vm.Available.Single(r => r.Variant.Build == "b9221");
        await vm.InstallVariantCommand.ExecuteAsync(olderRow);
        await vm.InstallVariantCommand.ExecuteAsync(newerRow);

        // Simulate a successful probe of the b9204 server currently in memory.
        vm.IsConnected = true;
        vm.BuildInfo = "b9204-726704a16";

        // User picks the newer install as the next-launch active.
        var newerInstall = vm.Installed.Single(i => i.Build == "b9221");
        await vm.SetActiveManagedCommand.ExecuteAsync(newerInstall);

        Assert.Equal("b9221", vm.ActiveManagedInstall?.Build);
        Assert.Equal("b9204", vm.RunningManagedInstall?.Build);
        Assert.Equal(LlamaServerBackend.Cuda13, vm.RunningManagedInstall?.Backend);
        Assert.True(vm.HasRunningManagedInstall);
    }

    [AvaloniaFact]
    public async Task RunningManagedInstall_NullWhenProbeDisconnected()
    {
        var older = SampleVariant(build: "b9204", backend: LlamaServerBackend.Cuda13);
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9204", new[] { older }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count > 0);
        await vm.InstallVariantCommand.ExecuteAsync(vm.Available.Single());

        // No live probe — disconnected.
        vm.IsConnected = false;
        vm.BuildInfo = null;
        await vm.RefreshInstalledCommand.ExecuteAsync(null);

        Assert.False(vm.HasRunningManagedInstall);
        Assert.Null(vm.RunningManagedInstall);
    }

    [AvaloniaFact]
    public async Task RunningManagedInstall_NullWhenMultipleInstallsShareSameBuild()
    {
        // When two installs share the same build (CUDA + CPU at b9221), the probe's
        // build_info alone can't disambiguate backend, so RunningManagedInstall stays
        // null and the status panel falls back to the /props "Build:" line.
        var cudaVariant = VariantWithAsset(
            build: "b9221",
            backend: LlamaServerBackend.Cuda13,
            assetName: "llama-b9221-bin-win-cuda-13.1-x64.zip");
        var cpuVariant = VariantWithAsset(
            build: "b9221",
            backend: LlamaServerBackend.Cpu,
            assetName: "llama-b9221-bin-win-cpu-x64.zip");
        _catalog.SetGroups(new LlamaServerReleaseGroup("b9221", new[] { cudaVariant, cpuVariant }));
        var vm = NewVm();
        await WaitForAsync(() => vm.Available.Count >= 2);
        await vm.InstallVariantCommand.ExecuteAsync(
            vm.Available.Single(r => r.Variant.Backend == LlamaServerBackend.Cuda13));
        await vm.InstallVariantCommand.ExecuteAsync(
            vm.Available.Single(r => r.Variant.Backend == LlamaServerBackend.Cpu));
        Assert.Equal(2, vm.Installed.Count);

        vm.IsConnected = true;
        vm.BuildInfo = "b9221-deadbeef";
        await vm.RefreshInstalledCommand.ExecuteAsync(null);

        Assert.Null(vm.RunningManagedInstall);
        Assert.False(vm.HasRunningManagedInstall);
    }

    private static LlamaServerVariant VariantWithAsset(
        string build,
        LlamaServerBackend backend,
        string assetName) => new(
            Build: build,
            Backend: backend,
            Os: LlamaServerOs.Windows,
            Arch: LlamaServerArch.X64,
            AssetName: assetName,
            Bytes: 31_000_000,
            DownloadUrl: "https://example.test/v.zip",
            Sha256: null,
            CompanionAssetName: null,
            CompanionDownloadUrl: null,
            CompanionBytes: null,
            CompanionSha256: null);

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
