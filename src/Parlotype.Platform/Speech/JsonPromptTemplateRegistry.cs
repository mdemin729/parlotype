using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// File-backed implementation of <see cref="IPromptTemplateRegistry"/>. User
/// prompts live in <c>prompts.json</c> under <c>%LOCALAPPDATA%\parlotype</c>;
/// the active selection lives in <see cref="ISettingsService"/> under
/// <see cref="SettingsKeys.ActivePromptId"/>. The built-in default is merged in
/// at read time and is never written to disk, so it always exists even if the
/// file is hand-edited or deleted.
/// </summary>
public sealed class JsonPromptTemplateRegistry : IPromptTemplateRegistry
{
    internal const string FileName = "prompts.json";
    internal const string BuiltInDefaultId = "builtin-default";

    /// <summary>
    /// The built-in default prompt — Google's prescribed verbatim transcription prompt,
    /// plus an explicit "Use punctuation." instruction in every body: without it the
    /// model punctuates inconsistently.
    /// </summary>
    internal static readonly PromptTemplate BuiltInDefault = new(
        Id: BuiltInDefaultId,
        Name: "Default (verbatim transcription)",
        Text: "Transcribe the following speech segment in {speech_lang} into {speech_lang} text. " +
              "Use punctuation. " +
              "Only output the transcription, with no newlines. " +
              "When transcribing numbers, write the digits.",
        IsBuiltIn: true,
        TranslationText:
            "Transcribe the following speech segment spoken in {speech_lang} and translate it into {text_lang}. " +
            "Use punctuation. " +
            "Only output the {text_lang} text, with no newlines. " +
            "When writing numbers, write the digits.",
        AutoDetectText:
            "Detect the language being spoken and transcribe the speech in that same language. " +
            "Use punctuation. " +
            "Only output the transcription, with no newlines. " +
            "When transcribing numbers, write the digits.");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _directory;
    private readonly ISettingsService _settings;
    private readonly ILogger<JsonPromptTemplateRegistry> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonPromptTemplateRegistry(
        ISettingsService settings,
        ILogger<JsonPromptTemplateRegistry> logger)
        : this(DefaultDirectory(), settings, logger) { }

    internal JsonPromptTemplateRegistry(
        string directory,
        ISettingsService settings,
        ILogger<JsonPromptTemplateRegistry> logger)
    {
        _directory = directory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary><c>%LOCALAPPDATA%\parlotype</c>.</summary>
    public static string DefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype");

    public async Task<IReadOnlyList<PromptTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return Merge(await LoadUserPromptsAsync(cancellationToken));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PromptTemplate?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (id == BuiltInDefaultId)
            return BuiltInDefault;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUserPromptsAsync(cancellationToken);
            return users.FirstOrDefault(p => p.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddOrUpdateAsync(PromptTemplate prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt.Id);

        if (prompt.IsBuiltIn || prompt.Id == BuiltInDefaultId)
            throw new InvalidOperationException("Built-in prompts cannot be modified.");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUserPromptsAsync(cancellationToken);
            var entry = prompt with { IsBuiltIn = false };
            var index = users.FindIndex(p => p.Id == prompt.Id);
            if (index >= 0)
                users[index] = entry;
            else
                users.Add(entry);

            await SaveUserPromptsAsync(users, cancellationToken);
            _logger.LogInformation("Prompt saved: {Id} ({Name})", prompt.Id, prompt.Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (id == BuiltInDefaultId)
            throw new InvalidOperationException("The built-in prompt cannot be deleted.");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUserPromptsAsync(cancellationToken);
            if (users.RemoveAll(p => p.Id == id) > 0)
            {
                await SaveUserPromptsAsync(users, cancellationToken);
                _logger.LogInformation("Prompt removed: {Id}", id);
            }
        }
        finally
        {
            _lock.Release();
        }

        // Reset the active selection if it pointed at the removed prompt.
        var active = await _settings.GetAsync<string>(SettingsKeys.ActivePromptId, cancellationToken);
        if (active == id)
            await _settings.SetAsync(SettingsKeys.ActivePromptId, BuiltInDefaultId, cancellationToken);
    }

    public async Task<PromptTemplate> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var activeId = await _settings.GetAsync<string>(SettingsKeys.ActivePromptId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(activeId))
        {
            var match = await GetAsync(activeId, cancellationToken);
            if (match is not null)
                return match;
        }

        return BuiltInDefault;
    }

    public async Task SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _settings.SetAsync(SettingsKeys.ActivePromptId, id, cancellationToken);
        _logger.LogInformation("Active prompt set to: {Id}", id);
    }

    private static List<PromptTemplate> Merge(List<PromptTemplate> users)
    {
        var result = new List<PromptTemplate> { BuiltInDefault };
        result.AddRange(users.Where(p => p.Id != BuiltInDefaultId).Select(p => p with { IsBuiltIn = false }));
        return result;
    }

    private async Task<List<PromptTemplate>> LoadUserPromptsAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_directory, FileName);
        if (!File.Exists(path))
            return [];

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read prompts file at {Path}", path);
            return [];
        }

        try
        {
            var prompts = JsonSerializer.Deserialize<List<PromptTemplate>>(json, JsonOptions);
            return prompts is null
                ? []
                : prompts.Where(p => p.Id != BuiltInDefaultId).ToList();
        }
        catch (JsonException ex)
        {
            QuarantineCorruptFile(path, ex);
            return [];
        }
    }

    private async Task SaveUserPromptsAsync(List<PromptTemplate> users, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, FileName);
        var json = JsonSerializer.Serialize(users, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private void QuarantineCorruptFile(string path, Exception cause)
    {
        var backupPath = $"{path}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        try
        {
            File.Move(path, backupPath, overwrite: false);
            _logger.LogWarning(cause,
                "Prompts file at {Path} was corrupt; moved to {Backup} and starting fresh.",
                path, backupPath);
        }
        catch (Exception moveEx)
        {
            _logger.LogError(moveEx,
                "Prompts file at {Path} was corrupt and could not be moved aside.", path);
        }
    }
}
