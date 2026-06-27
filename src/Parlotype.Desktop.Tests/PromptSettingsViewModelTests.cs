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
        vm.EditingText = "Transcribe {speech_lang}.";
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

    [Fact]
    public async Task EditPrompt_OnUserPrompt_EntersEditModeWithPopulatedFields()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(new PromptTemplate("u1", "Custom Name", "Custom text here"), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(registry);

        vm.EditPromptCommand.Execute("u1");

        Assert.True(vm.IsEditing);
        Assert.Equal("Custom Name", vm.EditingName);
        Assert.Equal("Custom text here", vm.EditingText);
    }

    [Fact]
    public async Task SavePrompt_WithEditingId_UpdatesExistingPrompt()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(new PromptTemplate("u1", "Old Name", "Old text"), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(registry);

        vm.EditPromptCommand.Execute("u1");
        vm.EditingName = "New Name";
        vm.EditingText = "New text";
        await vm.SavePromptCommand.ExecuteAsync(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(2, vm.Prompts.Count);
        var updated = vm.Prompts.Single(p => p.Id == "u1");
        Assert.Equal("New Name", updated.Name);
        var stored = await registry.GetAsync("u1", TestContext.Current.CancellationToken);
        Assert.Equal("New Name", stored!.Name);
    }

    [Fact]
    public async Task CancelEdit_ClearsIsEditingAndDoesNotSave()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(new PromptTemplate("u1", "Original", "Original text"), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(registry);

        vm.EditPromptCommand.Execute("u1");
        vm.CancelEditCommand.Execute(null);

        Assert.False(vm.IsEditing);

        // After cancel, _editingId should be null — a subsequent save must create a new entry, not update u1
        vm.EditingName = "Brand New";
        vm.EditingText = "New text";
        await vm.SavePromptCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Prompts.Count);
        Assert.Contains(vm.Prompts, p => p.Name == "Brand New");
        Assert.Contains(vm.Prompts, p => p.Id == "u1" && p.Name == "Original");
    }

    [Fact]
    public void NewPrompt_PrePopulatesEditingTextWithSpeechLanguageToken()
    {
        var vm = BuildViewModel();

        vm.NewPromptCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Empty(vm.EditingName);
        Assert.Contains(PromptTemplate.SpeechLanguageToken, vm.EditingText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectPrompt_SameId_IsNoOp()
    {
        var registry = new MockPromptTemplateRegistry();
        var vm = BuildViewModel(registry);

        await vm.SelectPromptCommand.ExecuteAsync(MockPromptTemplateRegistry.BuiltInId);

        Assert.Equal(MockPromptTemplateRegistry.BuiltInId, vm.SelectedPromptId);
        Assert.Equal(0, registry.SetActiveCallCount);
        Assert.Single(vm.Prompts);
    }
}
