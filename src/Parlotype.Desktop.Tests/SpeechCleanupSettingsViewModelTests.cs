using System.Globalization;
using Avalonia.Headless.XUnit;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The Speech cleanup page (ADR-073). The switch is the whole setup, so most of
/// these are about what it fetches, when it gives up, and that it never stays
/// on in a state where cleanup cannot work.
/// </summary>
/// <remarks>
/// Every test is an <c>[AvaloniaFact]</c>: a language switch raises
/// <c>Localizer.CultureChanged</c>, which reaches views other test classes left
/// bound to the singleton, and those may only be touched from the UI thread.
/// </remarks>
[Collection(CultureBoundTests.Name)]
public sealed class SpeechCleanupSettingsViewModelTests : IDisposable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    private readonly MockSettingsService _settings = new();
    private readonly MockSpeechCleanupModelInstaller _model = new();
    private readonly MockLlamaServerRegistry _registry = new();
    private readonly MockLlamaServerCatalog _catalog = new();
    private readonly MockLlamaServerInstaller _serverInstaller;
    private readonly MockSpeechCleanupService _cleanup;

    public SpeechCleanupSettingsViewModelTests()
    {
        Localizer.Instance.SetCulture(English);
        _serverInstaller = new MockLlamaServerInstaller(_registry);
        _cleanup = new MockSpeechCleanupService(_model, _registry);
        _catalog.SetGroups(new LlamaServerReleaseGroup("b200",
        [
            Variant("b200", LlamaServerBackend.Cuda13),
            Variant("b200", LlamaServerBackend.Cpu),
        ]));
    }

    public void Dispose() => Localizer.Instance.SetCulture(English);

    private static LlamaServerVariant Variant(string build, LlamaServerBackend backend) => new(
        Build: build,
        Backend: backend,
        Os: LlamaServerOs.Windows,
        Arch: LlamaServerArch.X64,
        AssetName: $"llama-{build}-bin-win-{backend.ToString().ToLowerInvariant()}-x64.zip",
        Bytes: 1,
        DownloadUrl: "https://example.invalid/asset.zip",
        Sha256: null,
        CompanionAssetName: null,
        CompanionDownloadUrl: null,
        CompanionBytes: null,
        CompanionSha256: null);

    private async Task<SpeechCleanupSettingsViewModel> NewViewModelAsync()
    {
        var vm = new SpeechCleanupSettingsViewModel(
            _settings, _cleanup, _model, _registry, _catalog, _serverInstaller);
        await vm.Initialization;
        return vm;
    }

    private static async Task TurnOnAsync(SpeechCleanupSettingsViewModel vm)
    {
        vm.IsEnabled = true;
        await vm.PendingToggle;
    }

    private async Task<string?> SavedAsync(string key) =>
        await _settings.GetAsync<string>(key, TestContext.Current.CancellationToken);

    // --- Initial state ---

    [AvaloniaFact]
    public async Task Fresh_IsOff_SemiFormal_AndSaysWhatTurningItOnDownloads()
    {
        var vm = await NewViewModelAsync();

        Assert.False(vm.IsEnabled);
        Assert.False(vm.IsModelInstalled);
        Assert.Equal(CleanupStyling.SemiFormal, vm.SelectedStyling);
        Assert.True(vm.StylingOptions.Single(o => o.Styling == CleanupStyling.SemiFormal).IsSelected);
        Assert.Contains("462 MB", vm.StatusText);
        Assert.Null(vm.ErrorDetail);
    }

    [AvaloniaFact]
    public async Task ShowsTheModelUnderTheNameItsLicenceRequires()
    {
        var vm = await NewViewModelAsync();

        Assert.Equal("S1-mini by Superwhisper", vm.ModelName);
    }

    [AvaloniaFact]
    public async Task SavedSettings_AreHonoredOnInit_WithoutRunningSetup()
    {
        _model.Installed = true;
        _registry.AddManaged("b1-win-cpu-x64");
        await _registry.SetActiveAsync("b1-win-cpu-x64", LlamaServerSource.Managed, TestContext.Current.CancellationToken);
        await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, "True", TestContext.Current.CancellationToken);
        await _settings.SetAsync(SettingsKeys.SpeechCleanupStyling, "Formal", TestContext.Current.CancellationToken);
        await _settings.SetAsync(SettingsKeys.SpeechCleanupStructure, "Lists", TestContext.Current.CancellationToken);
        await _settings.SetAsync(SettingsKeys.SpeechCleanupContext, "Email", TestContext.Current.CancellationToken);

        var vm = await NewViewModelAsync();

        Assert.True(vm.IsEnabled);
        Assert.Equal(CleanupStyling.Formal, vm.SelectedStyling);
        Assert.True(vm.ListsEnabled);
        Assert.True(vm.EmailLayoutEnabled);
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_Ready, vm.StatusText);
        Assert.Equal(0, _model.InstallRequests);
        Assert.Empty(_serverInstaller.InstalledVariants);
    }

    [AvaloniaFact]
    public async Task SavedOn_ButTheModelIsGone_SaysItIsNotWorking()
    {
        await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, "True", TestContext.Current.CancellationToken);

        var vm = await NewViewModelAsync();

        Assert.True(vm.IsEnabled);
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_Broken, vm.StatusText);
    }

    // --- Turning it on ---

    [AvaloniaFact]
    public async Task TurnOn_WithNothingInstalled_FetchesTheModelAndACpuServer()
    {
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.True(vm.IsEnabled);
        Assert.True(vm.IsModelInstalled);
        Assert.Equal(1, _model.InstallRequests);
        var installed = Assert.Single(_serverInstaller.InstalledVariants);
        Assert.Equal(LlamaServerBackend.Cpu, installed.Backend);
        Assert.NotNull(_registry.ActiveId);
        Assert.Equal("True", await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_Ready, vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [AvaloniaFact]
    public async Task TurnOn_WithEverythingInstalled_DownloadsNothing()
    {
        _model.Installed = true;
        _registry.AddManaged("b1-win-cuda-x64");
        await _registry.SetActiveAsync("b1-win-cuda-x64", LlamaServerSource.Managed, TestContext.Current.CancellationToken);
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.True(vm.IsEnabled);
        Assert.Equal(0, _model.InstallRequests);
        Assert.Empty(_serverInstaller.InstalledVariants);
        Assert.Equal(0, _catalog.FetchCallCount);
        Assert.Equal("b1-win-cuda-x64", _registry.ActiveId);
    }

    [AvaloniaFact]
    public async Task TurnOn_ReusesAnInstallAlreadyOnDisk_InsteadOfDownloadingAnother()
    {
        // On disk but not selected: e.g. the active selector went stale.
        _model.Installed = true;
        _registry.AddManaged("b9-win-cuda-x64");
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.True(vm.IsEnabled);
        Assert.Equal("b9-win-cuda-x64", _registry.ActiveId);
        Assert.Empty(_serverInstaller.InstalledVariants);
        Assert.Equal(0, _catalog.FetchCallCount);
    }

    [AvaloniaFact]
    public async Task TurnOn_ModelDownloadDeclined_SwitchGoesBackOff_AndNothingIsSaved()
    {
        _model.UserAccepts = false;
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.False(vm.IsEnabled);
        Assert.Null(await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Empty(_serverInstaller.InstalledVariants);
        Assert.Null(vm.ErrorDetail);
        Assert.Contains("462 MB", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task TurnOn_ServerInstallCancelled_SwitchGoesBackOff_WithNoError()
    {
        _serverInstaller.InstallError = new OperationCanceledException();
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.False(vm.IsEnabled);
        Assert.Null(await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Null(vm.ErrorDetail);
        // The model did download; the page shows that rather than hiding it.
        Assert.True(vm.IsModelInstalled);
    }

    [AvaloniaFact]
    public async Task TurnOn_CatalogUnreachable_ShowsTheSystemsOwnError_AndStaysOff()
    {
        _catalog.FetchError = new HttpRequestException("No such host is known. (api.github.com:443)");
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.False(vm.IsEnabled);
        Assert.Null(await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_SetupFailed, vm.StatusText);
        Assert.Equal("No such host is known. (api.github.com:443)", vm.ErrorDetail);
    }

    [AvaloniaFact]
    public async Task TurnOn_NoCpuBuildInTheCatalog_FailsWithoutShowingOurOwnEnglish()
    {
        _catalog.SetGroups(new LlamaServerReleaseGroup("b200", [Variant("b200", LlamaServerBackend.Cuda13)]));
        var vm = await NewViewModelAsync();

        await TurnOnAsync(vm);

        Assert.False(vm.IsEnabled);
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_SetupFailed, vm.StatusText);
        Assert.Null(vm.ErrorDetail);
    }

    [AvaloniaFact]
    public async Task FailedSetup_StaysOnScreen_WhenThePageIsReopened()
    {
        _catalog.FetchError = new HttpRequestException("offline");
        var vm = await NewViewModelAsync();
        await TurnOnAsync(vm);

        await vm.RefreshAsync();

        Assert.Equal(Strings.Settings_SpeechCleanup_Status_SetupFailed, vm.StatusText);
        Assert.Equal("offline", vm.ErrorDetail);
    }

    // --- Turning it off, deleting the model ---

    [AvaloniaFact]
    public async Task TurnOff_SavesIt_AndFreesTheModel()
    {
        var vm = await NewViewModelAsync();
        await TurnOnAsync(vm);

        vm.IsEnabled = false;
        await vm.PendingToggle;

        Assert.Equal("False", await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Equal(1, _cleanup.UnloadCount);
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_Ready, vm.StatusText);
    }

    [AvaloniaFact]
    public async Task DeleteModel_WhileOn_TurnsCleanupOff()
    {
        var vm = await NewViewModelAsync();
        await TurnOnAsync(vm);

        await vm.DeleteModelCommand.ExecuteAsync(null);

        Assert.False(vm.IsEnabled);
        Assert.False(vm.IsModelInstalled);
        Assert.Equal(1, _model.DeleteCount);
        Assert.Equal(1, _cleanup.UnloadCount);
        Assert.Equal("False", await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
        Assert.Contains("462 MB", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task DownloadModel_OnItsOwn_DoesNotTurnCleanupOn()
    {
        var vm = await NewViewModelAsync();

        await vm.DownloadModelCommand.ExecuteAsync(null);

        Assert.True(vm.IsModelInstalled);
        Assert.False(vm.IsEnabled);
        Assert.Null(await SavedAsync(SettingsKeys.SpeechCleanupEnabled));
    }

    // --- Style options ---

    [AvaloniaFact]
    public async Task SelectingAStyle_MarksItAndSavesIt()
    {
        var vm = await NewViewModelAsync();

        vm.SelectStylingCommand.Execute(CleanupStyling.Casual);

        Assert.Equal(CleanupStyling.Casual, vm.SelectedStyling);
        Assert.True(vm.StylingOptions.Single(o => o.Styling == CleanupStyling.Casual).IsSelected);
        Assert.False(vm.StylingOptions.Single(o => o.Styling == CleanupStyling.SemiFormal).IsSelected);
        Assert.Equal("Casual", await SavedAsync(SettingsKeys.SpeechCleanupStyling));
    }

    [AvaloniaFact]
    public async Task ListsAndEmailSwitches_SaveTheTrainedValues()
    {
        var vm = await NewViewModelAsync();

        vm.ListsEnabled = true;
        vm.EmailLayoutEnabled = true;

        Assert.Equal("Lists", await SavedAsync(SettingsKeys.SpeechCleanupStructure));
        Assert.Equal("Email", await SavedAsync(SettingsKeys.SpeechCleanupContext));

        vm.ListsEnabled = false;
        vm.EmailLayoutEnabled = false;

        Assert.Equal("Prose", await SavedAsync(SettingsKeys.SpeechCleanupStructure));
        Assert.Equal("General", await SavedAsync(SettingsKeys.SpeechCleanupContext));
    }

    [AvaloniaFact]
    public async Task LoadingSavedSettings_DoesNotWriteThemBack()
    {
        await _settings.SetAsync(SettingsKeys.SpeechCleanupStyling, "Formal", TestContext.Current.CancellationToken);
        _settings.ThrowWritesFor.Add(SettingsKeys.SpeechCleanupStyling);
        _settings.ThrowWritesFor.Add(SettingsKeys.SpeechCleanupStructure);
        _settings.ThrowWritesFor.Add(SettingsKeys.SpeechCleanupContext);
        _settings.ThrowWritesFor.Add(SettingsKeys.SpeechCleanupEnabled);

        var vm = await NewViewModelAsync();

        Assert.Equal(CleanupStyling.Formal, vm.SelectedStyling);
    }

    // --- Live language switch ---

    [AvaloniaFact]
    public async Task LanguageChange_RewordsTheStatusAndTheStyleCards()
    {
        var vm = await NewViewModelAsync();
        var englishStatus = vm.StatusText;
        var englishStyle = vm.StylingOptions[0].DisplayName;
        var englishDescription = vm.StylingOptions[0].Description;

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ru"));

        Assert.NotEqual(englishStatus, vm.StatusText);
        Assert.Contains("462 MB", vm.StatusText);
        Assert.NotEqual(englishStyle, vm.StylingOptions[0].DisplayName);
        Assert.NotEqual(englishDescription, vm.StylingOptions[0].Description);
        Assert.Equal("S1-mini by Superwhisper", vm.ModelName);
    }

    [AvaloniaFact]
    public async Task LanguageChange_LeavesTheSystemsErrorTextAsItArrived()
    {
        _catalog.FetchError = new HttpRequestException("No such host is known.");
        var vm = await NewViewModelAsync();
        await TurnOnAsync(vm);

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ru"));

        Assert.Equal("No such host is known.", vm.ErrorDetail);
        Assert.Equal(Strings.Settings_SpeechCleanup_Status_SetupFailed, vm.StatusText);
    }

    /// <summary>
    /// Parity checks cannot see a style added without its keys: it would be
    /// missing from every language at once.
    /// </summary>
    [AvaloniaFact]
    public async Task EveryStyle_HasATranslatedNameAndDescription()
    {
        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ru"));
        var vm = await NewViewModelAsync();

        Assert.Equal(Enum.GetValues<CleanupStyling>().Length, vm.StylingOptions.Length);
        foreach (var option in vm.StylingOptions)
        {
            Assert.Contains(option.DisplayName, c => c is >= 'а' and <= 'я');
            Assert.Contains(option.Description, c => c is >= 'а' and <= 'я');
        }
    }
}
