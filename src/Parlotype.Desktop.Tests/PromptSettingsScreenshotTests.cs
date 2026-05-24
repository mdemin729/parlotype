using Avalonia.Headless.XUnit;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class PromptSettingsScreenshotReportFixture : IAsyncLifetime
{
    private readonly List<Scenario> _scenarios = [];

    public void AddScenario(Scenario scenario)
    {
        lock (_scenarios)
            _scenarios.Add(scenario);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        List<Scenario> snapshot;
        lock (_scenarios)
            snapshot = [.. _scenarios];

        if (snapshot.Count > 0)
        {
            var reportPath = Path.Combine(
                ScreenshotReportFixture.FindRepoRoot(), "reports", "prompt-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Prompt Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class PromptSettingsScreenshotTests : IClassFixture<PromptSettingsScreenshotReportFixture>
{
    private readonly PromptSettingsScreenshotReportFixture _report;

    public PromptSettingsScreenshotTests(PromptSettingsScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultOnlyBuiltIn()
    {
        var registry = new MockPromptTemplateRegistry();
        var vm = new PromptSettingsViewModel(registry);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new PromptSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default state: only the built-in 'Default (verbatim transcription)' prompt is present. " +
            "It is selected (accent-colored indicator visible). The 'Built-in' badge is shown. " +
            "No Edit or Delete buttons appear on the built-in row. A 'New prompt' button is at the bottom.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default — Built-in Only",
            "Fresh install — one built-in prompt, selected, no edit/delete actions visible.",
            steps));

        Assert.Single(vm.Prompts);
        Assert.True(vm.Prompts[0].IsBuiltIn);
        Assert.Equal(MockPromptTemplateRegistry.BuiltInId, vm.SelectedPromptId);
    }

    [AvaloniaFact]
    public async Task Scenario_MultiplePromptsUserSelected()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(
            new PromptTemplate("u1", "Formal transcription", "Transcribe formally in {language}."),
            CancellationToken.None);
        await registry.AddOrUpdateAsync(
            new PromptTemplate("u2", "Medical dictation", "Transcribe medical speech in {language} verbatim."),
            CancellationToken.None);
        var vm = new PromptSettingsViewModel(registry);
        await SettleAsync();

        await vm.SelectPromptCommand.ExecuteAsync("u2");
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new PromptSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Three prompts listed: built-in (no Edit/Delete), 'Formal transcription' (Edit + Delete visible), " +
            "'Medical dictation' (selected — accent indicator shown, Edit + Delete visible). " +
            "The 'New prompt' button is below the list.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Multiple Prompts — User Prompt Selected",
            "Two user prompts added; 'Medical dictation' is the active selection.",
            steps));

        Assert.Equal(3, vm.Prompts.Count);
        Assert.Equal("u2", vm.SelectedPromptId);
        Assert.True(vm.Prompts.Single(p => p.Id == "u2").IsSelected);
    }

    [AvaloniaFact]
    public async Task Scenario_NewPromptEditor()
    {
        var registry = new MockPromptTemplateRegistry();
        var vm = new PromptSettingsViewModel(registry);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: initial list
        var view1 = new PromptSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: only the built-in prompt. The 'New prompt' button is visible at the bottom.",
            screenshot1));

        // Step 2: click New
        vm.NewPromptCommand.Execute(null);
        await SettleAsync();

        var view2 = new PromptSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User clicks 'New prompt'. The list is hidden and the inline editor appears. " +
            "The Name field is empty. The Prompt text field is pre-populated with the default template " +
            "(containing '{language}'). A tip below reads 'Tip: use {language} where the source language " +
            "should appear.' Save and Cancel buttons are visible.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "New Prompt — Inline Editor",
            "User initiates a new prompt; editor opens with template text pre-filled, name blank.",
            steps));

        Assert.True(vm.IsEditing);
        Assert.Empty(vm.EditingName);
        Assert.Contains("{language}", vm.EditingText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Scenario_EditExistingPromptMode()
    {
        var registry = new MockPromptTemplateRegistry();
        await registry.AddOrUpdateAsync(
            new PromptTemplate("u1", "My Custom Prompt", "Transcribe carefully in {language}."),
            CancellationToken.None);
        var vm = new PromptSettingsViewModel(registry);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: list view with edit button
        var view1 = new PromptSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "List view: built-in prompt (no Edit/Delete) and 'My Custom Prompt' with Duplicate, Edit, and Delete buttons.",
            screenshot1));

        // Step 2: enter edit mode
        vm.EditPromptCommand.Execute("u1");
        await SettleAsync();

        var view2 = new PromptSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User clicks Edit on 'My Custom Prompt'. The inline editor opens pre-filled: " +
            "Name field shows 'My Custom Prompt', Prompt text field shows the existing text. " +
            "Saving will overwrite the same entry. Cancel returns to the list without changes.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Edit Existing Prompt",
            "User edits a user-created prompt; editor opens pre-populated with the prompt's current name and text.",
            steps));

        Assert.True(vm.IsEditing);
        Assert.Equal("My Custom Prompt", vm.EditingName);
        Assert.Equal("Transcribe carefully in {language}.", vm.EditingText);
    }
}
