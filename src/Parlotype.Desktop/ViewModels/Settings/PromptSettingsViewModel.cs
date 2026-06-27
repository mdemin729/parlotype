using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Lets the user create, edit, duplicate, delete, and select transcription
/// prompts for the Gemma 4 engine. The built-in default is always present and
/// cannot be edited or deleted. Editing happens inline via an
/// <see cref="IsEditing"/>-toggled form.
/// </summary>
public partial class PromptSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly IPromptTemplateRegistry _registry;
    private readonly ILogger<PromptSettingsViewModel> _logger;

    public override string Title => "Prompts";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Gemma4;

    public ObservableCollection<PromptDisplayItem> Prompts { get; } = [];

    [ObservableProperty]
    private string _selectedPromptId = string.Empty;

    /// <summary>When true, the inline editor is shown instead of the list actions.</summary>
    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editingName = string.Empty;

    [ObservableProperty]
    private string _editingText = string.Empty;

    // Id being edited; null means a brand-new prompt.
    private string? _editingId;

    public PromptSettingsViewModel(
        IPromptTemplateRegistry registry,
        ILogger<PromptSettingsViewModel>? logger = null)
    {
        _registry = registry;
        _logger = logger ?? NullLogger<PromptSettingsViewModel>.Instance;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var active = await _registry.GetActiveAsync();
        SelectedPromptId = active.Id;

        var prompts = await _registry.ListAsync();
        Prompts.Clear();
        foreach (var p in prompts)
        {
            Prompts.Add(new PromptDisplayItem(
                p, SelectPromptCommand, EditPromptCommand, DuplicatePromptCommand, DeletePromptCommand)
            {
                IsSelected = p.Id == SelectedPromptId,
            });
        }
    }

    [RelayCommand]
    private async Task SelectPromptAsync(string id)
    {
        if (id == SelectedPromptId)
            return;

        await _registry.SetActiveAsync(id);
        SelectedPromptId = id;
        foreach (var item in Prompts)
            item.IsSelected = item.Id == id;

        _logger.LogInformation("Active prompt selected: {Id}", id);
    }

    [RelayCommand]
    private void NewPrompt()
    {
        _editingId = null;
        EditingName = string.Empty;
        EditingText = $"Transcribe the following speech in {PromptTemplate.SpeechLanguageToken} verbatim. " +
                      "Only output the transcription.";
        IsEditing = true;
    }

    [RelayCommand]
    private void EditPrompt(string id)
    {
        var item = Prompts.FirstOrDefault(p => p.Id == id);
        if (item is null || !item.CanEdit)
            return;

        _editingId = id;
        EditingName = item.Name;
        EditingText = item.Text;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task DuplicatePromptAsync(string id)
    {
        var source = await _registry.GetAsync(id);
        if (source is null)
            return;

        var copy = new PromptTemplate(
            Id: Guid.NewGuid().ToString("n"),
            Name: $"{source.Name} (copy)",
            Text: source.Text);

        await _registry.AddOrUpdateAsync(copy);
        await LoadAsync();
        _logger.LogInformation("Prompt duplicated from {SourceId} to {NewId}", id, copy.Id);
    }

    [RelayCommand]
    private async Task DeletePromptAsync(string id)
    {
        var item = Prompts.FirstOrDefault(p => p.Id == id);
        if (item is null || !item.CanEdit)
            return;

        await _registry.RemoveAsync(id);
        await LoadAsync();
        _logger.LogInformation("Prompt deleted: {Id}", id);
    }

    [RelayCommand]
    private async Task SavePromptAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingName) || string.IsNullOrWhiteSpace(EditingText))
            return;

        var prompt = new PromptTemplate(
            Id: _editingId ?? Guid.NewGuid().ToString("n"),
            Name: EditingName.Trim(),
            Text: EditingText.Trim());

        await _registry.AddOrUpdateAsync(prompt);
        IsEditing = false;
        await LoadAsync();
        _logger.LogInformation("Prompt saved: {Id}", prompt.Id);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        _editingId = null;
    }
}
