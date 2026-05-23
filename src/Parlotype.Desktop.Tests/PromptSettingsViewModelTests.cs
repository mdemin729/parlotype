using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class PromptSettingsViewModelTests
{
    private static PromptSettingsViewModel BuildViewModel(MockPromptTemplateRegistry? registry = null) =>
        new(registry ?? new MockPromptTemplateRegistry());

    [Fact]
    public void Constructor_LoadsBuiltIn_AndMarksItNonEditable()
    {
        var vm = BuildViewModel();

        var builtIn = Assert.Single(vm.Prompts);
        Assert.True(builtIn.IsBuiltIn);
        Assert.False(builtIn.CanEdit);
        Assert.True(builtIn.IsSelected);
        Assert.Equal(MockPromptTemplateRegistry.BuiltInId, vm.SelectedPromptId);
    }

    [Fact]
    public void RestrictedToGemma4()
    {
        var vm = BuildViewModel();
        Assert.Equal(SpeechEngine.Gemma4, vm.RestrictToEngine);
        Assert.Equal(SettingsCategory.SpeechEngine, vm.Category);
        Assert.Equal("Prompts", vm.Title);
    }

    [Fact]
    public async Task SavePrompt_AddsNewUserPrompt()
    {
        var registry = new MockPromptTemplateRegistry();
        var vm = BuildViewModel(registry);

        vm.NewPromptCommand.Execute(null);
        Assert.True(vm.IsEditing);

        vm.EditingName = "My prompt";
        vm.EditingText = "Transcribe {language}.";
        await vm.SavePromptCommand.ExecuteAsync(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(2, vm.Prompts.Count);
        Assert.Contains(vm.Prompts, p => p.Name == "My prompt" && p.CanEdit);
    }

    [Fact]
    public async Task SavePrompt_WithBlankFields_DoesNothing()
    {
        var vm = BuildViewModel();

        vm.NewPromptCommand.Execute(null);
        vm.EditingName = "   ";
        vm.EditingText = "";
        await vm.SavePromptCommand.ExecuteAsync(null);

        Assert.Single(vm.Prompts);
    }

    [Fact]
    public async Task DeletePrompt_RemovesUserPrompt()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(new PromptTemplate("u1", "Custom", "X"), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(registry);

        await vm.DeletePromptCommand.ExecuteAsync("u1");

        Assert.Single(vm.Prompts);
        Assert.DoesNotContain(vm.Prompts, p => p.Id == "u1");
    }

    [Fact]
    public async Task SelectPrompt_UpdatesActiveSelection()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(new PromptTemplate("u1", "Custom", "X"), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(registry);

        await vm.SelectPromptCommand.ExecuteAsync("u1");

        Assert.Equal("u1", vm.SelectedPromptId);
        Assert.True(vm.Prompts.Single(p => p.Id == "u1").IsSelected);
        Assert.False(vm.Prompts.Single(p => p.IsBuiltIn).IsSelected);

        var active = await registry.GetActiveAsync(TestContext.Current.CancellationToken);
        Assert.Equal("u1", active.Id);
    }

    [Fact]
    public async Task DuplicatePrompt_CreatesCopy()
    {
        var registry = new MockPromptTemplateRegistry();
        var vm = BuildViewModel(registry);

        await vm.DuplicatePromptCommand.ExecuteAsync(MockPromptTemplateRegistry.BuiltInId);

        Assert.Equal(2, vm.Prompts.Count);
        Assert.Contains(vm.Prompts, p => p.Name.EndsWith("(copy)", StringComparison.Ordinal) && p.CanEdit);
    }

    [Fact]
    public void EditPrompt_OnBuiltIn_DoesNotEnterEditMode()
    {
        var vm = BuildViewModel();

        vm.EditPromptCommand.Execute(MockPromptTemplateRegistry.BuiltInId);

        Assert.False(vm.IsEditing);
    }
}
