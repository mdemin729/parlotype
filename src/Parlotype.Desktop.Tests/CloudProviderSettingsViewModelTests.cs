using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class CloudProviderSettingsViewModelTests
{
    [Fact]
    public async Task InitialLoad_NoSettings_FieldsAreBlank()
    {
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal("", vm.OpenAiBaseUrl);
        Assert.Equal("", vm.OpenAiModel);
        Assert.Equal("", vm.XaiBaseUrl);
        Assert.Equal("", vm.XaiModel);
        Assert.False(vm.HasOpenAiKey);
        Assert.False(vm.HasXaiKey);
        Assert.Equal("No key", vm.OpenAiKeyStatus);
        Assert.Equal("No key", vm.XaiKeyStatus);
    }

    [Fact]
    public async Task InitialLoad_LoadsPersistedBaseUrlAndModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.OpenAiCompatBaseUrl, "https://api.groq.com/openai/v1", ct);
        await settings.SetAsync(SettingsKeys.OpenAiCompatModel, "whisper-large-v3", ct);
        await settings.SetAsync(SettingsKeys.XaiGrokBaseUrl, "https://custom.xai.example/v1", ct);
        await settings.SetAsync(SettingsKeys.XaiGrokModel, "grok-stt-mini", ct);

        var vm = new CloudProviderSettingsViewModel(settings, new MockSecretStore());
        await Task.Delay(100, ct);

        Assert.Equal("https://api.groq.com/openai/v1", vm.OpenAiBaseUrl);
        Assert.Equal("whisper-large-v3", vm.OpenAiModel);
        Assert.Equal("https://custom.xai.example/v1", vm.XaiBaseUrl);
        Assert.Equal("grok-stt-mini", vm.XaiModel);
    }

    [Fact]
    public async Task InitialLoad_ExistingKeys_ReportsKeySaved()
    {
        var ct = TestContext.Current.CancellationToken;
        var secrets = new MockSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-test-123", ct);

        var vm = new CloudProviderSettingsViewModel(new MockSettingsService(), secrets);
        await Task.Delay(100, ct);

        Assert.True(vm.HasOpenAiKey);
        Assert.Equal("Key saved", vm.OpenAiKeyStatus);
        Assert.False(vm.HasXaiKey);
        Assert.Equal("No key", vm.XaiKeyStatus);
    }

    [Fact]
    public async Task ChangingBaseUrl_PersistsToSettings()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var vm = new CloudProviderSettingsViewModel(settings, new MockSecretStore());
        await Task.Delay(100, ct);

        vm.OpenAiBaseUrl = "https://api.groq.com/openai/v1";
        await Task.Delay(50, ct);

        Assert.Equal(
            "https://api.groq.com/openai/v1",
            await settings.GetAsync<string>(SettingsKeys.OpenAiCompatBaseUrl, ct));
    }

    [Fact]
    public async Task ChangingModel_PersistsToSettings()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var vm = new CloudProviderSettingsViewModel(settings, new MockSecretStore());
        await Task.Delay(100, ct);

        vm.XaiModel = "grok-stt-mini";
        await Task.Delay(50, ct);

        Assert.Equal("grok-stt-mini", await settings.GetAsync<string>(SettingsKeys.XaiGrokModel, ct));
    }

    [Fact]
    public async Task SaveOpenAiKey_CallsSecretStore_AndClearsEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await Task.Delay(100, ct);

        vm.OpenAiKeyEntry = "sk-newly-entered-key";
        await vm.SaveOpenAiKeyCommand.ExecuteAsync(null);

        Assert.Equal("sk-newly-entered-key", await secrets.GetAsync(SettingsKeys.OpenAiCompatApiKey, ct));
        Assert.Equal("", vm.OpenAiKeyEntry);
        Assert.True(vm.HasOpenAiKey);
        Assert.Equal("Key saved", vm.OpenAiKeyStatus);
    }

    [Fact]
    public async Task SaveOpenAiKey_BlankEntry_IsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await Task.Delay(100, ct);

        vm.OpenAiKeyEntry = "   ";
        await vm.SaveOpenAiKeyCommand.ExecuteAsync(null);

        Assert.Null(await secrets.GetAsync(SettingsKeys.OpenAiCompatApiKey, ct));
        Assert.False(vm.HasOpenAiKey);
    }

    [Fact]
    public async Task RemoveOpenAiKey_ClearsSecretAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-existing", ct);

        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await Task.Delay(100, ct);
        Assert.True(vm.HasOpenAiKey);

        await vm.RemoveOpenAiKeyCommand.ExecuteAsync(null);

        Assert.Null(await secrets.GetAsync(SettingsKeys.OpenAiCompatApiKey, ct));
        Assert.False(vm.HasOpenAiKey);
        Assert.Equal("No key", vm.OpenAiKeyStatus);
    }

    [Fact]
    public async Task SaveXaiKey_CallsSecretStore_AndClearsEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await Task.Delay(100, ct);

        vm.XaiKeyEntry = "xai-newly-entered-key";
        await vm.SaveXaiKeyCommand.ExecuteAsync(null);

        Assert.Equal("xai-newly-entered-key", await secrets.GetAsync(SettingsKeys.XaiGrokApiKey, ct));
        Assert.Equal("", vm.XaiKeyEntry);
        Assert.True(vm.HasXaiKey);
        Assert.Equal("Key saved", vm.XaiKeyStatus);
    }

    [Fact]
    public async Task RemoveXaiKey_ClearsSecretAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-existing", ct);

        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await Task.Delay(100, ct);
        Assert.True(vm.HasXaiKey);

        await vm.RemoveXaiKeyCommand.ExecuteAsync(null);

        Assert.Null(await secrets.GetAsync(SettingsKeys.XaiGrokApiKey, ct));
        Assert.False(vm.HasXaiKey);
        Assert.Equal("No key", vm.XaiKeyStatus);
    }

    [Fact]
    public void IsVisibleFor_CloudEngines_True()
    {
        var vm = new CloudProviderSettingsViewModel(new MockSettingsService(), new MockSecretStore());

        Assert.True(vm.IsVisibleFor(SpeechEngine.OpenAiCompatible));
        Assert.True(vm.IsVisibleFor(SpeechEngine.XaiGrok));
    }

    [Fact]
    public void IsVisibleFor_LocalEngines_False()
    {
        var vm = new CloudProviderSettingsViewModel(new MockSettingsService(), new MockSecretStore());

        Assert.False(vm.IsVisibleFor(SpeechEngine.Parakeet));
        Assert.False(vm.IsVisibleFor(SpeechEngine.Whisper));
        Assert.False(vm.IsVisibleFor(SpeechEngine.Gemma4));
    }
}
