using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Covers the two destructive controls on Settings → Application → Data. Every
/// test is rooted at a temp directory via <see cref="MockAppPaths"/>, so the real
/// user data folder is never reachable from here.
/// </summary>
public class DataSettingsViewModelTests
{
    private static DataSettingsViewModel Build(
        MockAppPaths paths,
        MockSettingsService settings,
        MockUserDialogService dialogs,
        MockShellService? shell = null) =>
        new(settings, paths, dialogs, shell ?? new MockShellService());

    [Fact]
    public async Task UninstallRemovesData_DefaultsToOff_WhenUnset()
    {
        using var paths = new MockAppPaths();
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService());

        await WaitForInitializationAsync(vm);

        // Off is the safe default: many uninstalls are really reinstalls, and
        // discarding several GB of models there is the wrong outcome.
        Assert.False(vm.UninstallRemovesData);
    }

    [Fact]
    public async Task UninstallRemovesData_LoadsSavedOptIn()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.UninstallRemovesUserData, "True", TestContext.Current.CancellationToken);

        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);

        Assert.True(vm.UninstallRemovesData);
    }

    [Fact]
    public async Task TogglingUninstallRemovesData_PersistsTheConsent()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);

        vm.UninstallRemovesData = true;

        // The uninstall hook reads settings.json directly, so the consent only
        // counts if it reaches the store.
        Assert.Equal(
            "True",
            await settings.GetAsync<string>(
                SettingsKeys.UninstallRemovesUserData, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TogglingUninstallRemovesData_ExposesTheWriteForShutdownToAwait()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);

        vm.UninstallRemovesData = true;
        await vm.PendingWrite;

        // Shutdown awaits this, so it must represent the write and not be left
        // as the already-completed placeholder.
        Assert.True(vm.PendingWrite.IsCompletedSuccessfully);
        Assert.Equal(
            "True",
            await settings.GetAsync<string>(
                SettingsKeys.UninstallRemovesUserData, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TurningCleanupOff_WhenTheWriteIsLost_RevertsTheToggleAndWarns()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.UninstallRemovesUserData, "True", TestContext.Current.CancellationToken);

        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);
        Assert.True(vm.UninstallRemovesData);

        // The dangerous direction: the user asks to keep their data, but the write
        // never lands, so uninstall would still delete it.
        settings.FailWritesFor.Add(SettingsKeys.UninstallRemovesUserData);
        vm.UninstallRemovesData = false;
        await vm.PendingWrite;

        // The toggle must not show a promise the disk does not back.
        Assert.True(vm.UninstallRemovesData);
        Assert.NotNull(vm.StatusMessage);
        Assert.Contains("still delete", vm.StatusMessage);
    }

    [Fact]
    public async Task TurningCleanupOn_WhenTheWriteThrows_RevertsToTheSafeState()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        settings.ThrowWritesFor.Add(SettingsKeys.UninstallRemovesUserData);

        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);

        vm.UninstallRemovesData = true;
        await vm.PendingWrite;

        Assert.False(vm.UninstallRemovesData);
        Assert.Contains("keep your data", vm.StatusMessage);
    }

    [Fact]
    public async Task RevertingTheToggle_DoesNotTriggerAnotherWrite()
    {
        using var paths = new MockAppPaths();
        var settings = new MockSettingsService();
        settings.ThrowWritesFor.Add(SettingsKeys.UninstallRemovesUserData);

        var vm = Build(paths, settings, new MockUserDialogService());
        await WaitForInitializationAsync(vm);

        vm.UninstallRemovesData = true;
        await vm.PendingWrite;

        // The revert must not re-enter PersistConsentAsync — if it did, the throwing
        // store would flip the toggle back and forth indefinitely.
        Assert.False(vm.UninstallRemovesData);
        Assert.True(vm.PendingWrite.IsCompleted);
    }

    [Fact]
    public async Task DeleteModels_WhenCancelled_DeletesNothing()
    {
        using var paths = new MockAppPaths();
        var model = paths.WriteFakeModel();
        var dialogs = new MockUserDialogService { ConfirmationResult = false };

        var vm = Build(paths, new MockSettingsService(), dialogs);
        await WaitForInitializationAsync(vm);

        await vm.DeleteModelsCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ShowConfirmationCount);
        Assert.True(File.Exists(model), "Cancelling the dialog must leave the models untouched.");
    }

    [Fact]
    public async Task DeleteModels_WhenConfirmed_RemovesTheModelsDirectory()
    {
        using var paths = new MockAppPaths();
        paths.WriteFakeModel();
        var dialogs = new MockUserDialogService { ConfirmationResult = true };

        var vm = Build(paths, new MockSettingsService(), dialogs);
        await WaitForInitializationAsync(vm);

        await vm.DeleteModelsCommand.ExecuteAsync(null);

        Assert.False(Directory.Exists(paths.ModelsDirectory));
        // Scoped to models — this is "reclaim disk space", not a factory reset.
        Assert.True(Directory.Exists(paths.DataDirectory));
    }

    [Fact]
    public async Task DeleteModels_AlwaysAsksFirst()
    {
        using var paths = new MockAppPaths();
        paths.WriteFakeModel();
        var dialogs = new MockUserDialogService { ConfirmationResult = true };

        var vm = Build(paths, new MockSettingsService(), dialogs);
        await WaitForInitializationAsync(vm);

        await vm.DeleteModelsCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ShowConfirmationCount);
        Assert.Contains("Delete", dialogs.LastConfirmText);
    }

    [Fact]
    public async Task ModelsSizeText_ReportsNothingDownloaded_ForAnEmptyCache()
    {
        using var paths = new MockAppPaths();
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService());

        await WaitForInitializationAsync(vm);

        Assert.Equal("Nothing downloaded", vm.ModelsSizeText);
    }

    [Fact]
    public async Task CopyPath_PutsTheDataDirectoryOnTheClipboard()
    {
        using var paths = new MockAppPaths();
        var shell = new MockShellService();
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService(), shell);
        await WaitForInitializationAsync(vm);

        await vm.CopyPathCommand.ExecuteAsync(null);

        Assert.Equal(1, shell.CopyTextCount);
        Assert.Equal(paths.DataDirectory, shell.LastCopiedText);
        Assert.Equal("Path copied to the clipboard.", vm.PathActionMessage);
    }

    [Fact]
    public async Task CopyPath_WhenTheClipboardIsUnavailable_SaysSo()
    {
        using var paths = new MockAppPaths();
        var shell = new MockShellService { Result = false };
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService(), shell);
        await WaitForInitializationAsync(vm);

        await vm.CopyPathCommand.ExecuteAsync(null);

        Assert.Equal("Could not copy the path.", vm.PathActionMessage);
    }

    [Fact]
    public async Task OpenFolder_OpensTheDataDirectory_AndStaysSilentOnSuccess()
    {
        using var paths = new MockAppPaths();
        var shell = new MockShellService();
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService(), shell);
        await WaitForInitializationAsync(vm);

        await vm.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal(1, shell.OpenDirectoryCount);
        Assert.Equal(paths.DataDirectory, shell.LastOpenedPath);
        // The opened window is its own feedback — no message needed.
        Assert.Null(vm.PathActionMessage);
    }

    [Fact]
    public async Task OpenFolder_WhenItFails_SaysSo()
    {
        using var paths = new MockAppPaths();
        var shell = new MockShellService { Result = false };
        var vm = Build(paths, new MockSettingsService(), new MockUserDialogService(), shell);
        await WaitForInitializationAsync(vm);

        await vm.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal("Could not open the folder.", vm.PathActionMessage);
    }

    /// <summary>
    /// The constructor kicks off loading as fire-and-forget (the pattern every
    /// settings section here uses), so give it a turn to land before asserting.
    /// </summary>
    private static async Task WaitForInitializationAsync(DataSettingsViewModel vm)
    {
        for (var i = 0; i < 50 && vm.ModelsSizeText == "—"; i++)
            await Task.Delay(10);
    }
}
