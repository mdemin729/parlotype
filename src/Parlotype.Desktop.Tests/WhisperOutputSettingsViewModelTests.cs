using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WhisperOutputSettingsViewModelTests
{
    [Fact]
    public async Task Init_DisablesTranslation_WhenSavedModelDoesNotSupportIt()
    {
        var settings = new MockSettingsService();
        var ct = TestContext.Current.CancellationToken;
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString(), ct);
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.LargeV3Turbo.ToString(), ct);

        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        Assert.False(vm.CanTranslate);
        // User preference is preserved even though the model can't translate.
        Assert.True(vm.TranslateToEnglishEnabled);
    }

    [Fact]
    public async Task Init_AllowsTranslation_WhenSavedModelSupportsIt()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.SelectedWhisperModel,
            WhisperModelType.Medium.ToString(),
            TestContext.Current.CancellationToken);

        var vm = new WhisperOutputSettingsViewModel(settings);
        await Task.Yield();

        Assert.True(vm.CanTranslate);
    }

    [Fact]
    public void UpdateTranslationAvailability_TogglesCanTranslate_WithoutChangingPreference()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings)
        {
            TranslateToEnglishEnabled = true,
        };

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        Assert.False(vm.CanTranslate);
        Assert.True(vm.TranslateToEnglishEnabled);

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3);
        Assert.True(vm.CanTranslate);
        Assert.True(vm.TranslateToEnglishEnabled);
    }

    [Fact]
    public void TranslationUnavailableNote_ReflectsPreservedIntent()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings)
        {
            TranslateToEnglishEnabled = true,
        };

        // Preference on → wording makes the paused/resume intent explicit.
        Assert.Contains("paused", vm.TranslationUnavailableNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resumes", vm.TranslationUnavailableNote, StringComparison.OrdinalIgnoreCase);

        // Preference off → plain "doesn't support" wording.
        vm.TranslateToEnglishEnabled = false;
        Assert.Contains("doesn't support", vm.TranslationUnavailableNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paused", vm.TranslationUnavailableNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TranslationUnavailableNote_RaisesPropertyChanged_WhenPreferenceChanges()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);

        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WhisperOutputSettingsViewModel.TranslationUnavailableNote))
                raised = true;
        };

        vm.TranslateToEnglishEnabled = !vm.TranslateToEnglishEnabled;

        Assert.True(raised);
    }

    [Fact]
    public void ShowTranslationPausedNote_TrueWhenCannotTranslateAndPreferenceOn()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings)
        {
            TranslateToEnglishEnabled = true,
        };

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);

        Assert.True(vm.ShowTranslationPausedNote);
        Assert.False(vm.ShowTranslationUnavailableNote);
    }

    [Fact]
    public void ShowTranslationUnavailableNote_TrueWhenCannotTranslateAndPreferenceOff()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings)
        {
            TranslateToEnglishEnabled = false,
        };

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);

        Assert.False(vm.ShowTranslationPausedNote);
        Assert.True(vm.ShowTranslationUnavailableNote);
    }

    [Fact]
    public void ShowTranslationPausedNoteAndUnavailable_BothFalse_WhenModelSupportsTranslation()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings)
        {
            TranslateToEnglishEnabled = true,
        };

        vm.UpdateTranslationAvailability(WhisperModelType.Medium);

        Assert.False(vm.ShowTranslationPausedNote);
        Assert.False(vm.ShowTranslationUnavailableNote);
    }

    [Fact]
    public void ShowTranslationPausedNote_RaisesPropertyChanged_WhenPreferenceChanges()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);

        var pausedRaised = false;
        var unavailableRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WhisperOutputSettingsViewModel.ShowTranslationPausedNote))
                pausedRaised = true;
            if (e.PropertyName == nameof(WhisperOutputSettingsViewModel.ShowTranslationUnavailableNote))
                unavailableRaised = true;
        };

        vm.TranslateToEnglishEnabled = !vm.TranslateToEnglishEnabled;

        Assert.True(pausedRaised);
        Assert.True(unavailableRaised);
    }
}
