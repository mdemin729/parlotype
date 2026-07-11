using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Records <see cref="ShowConfirmationAsync"/> calls and answers with a
/// scripted result, so ViewModel tests can assert a dialog was (or wasn't)
/// requested without any Avalonia window.
/// </summary>
public sealed class MockUserDialogService : IUserDialogService
{
    /// <summary>The value returned from every confirmation. Defaults to false (cancel).</summary>
    public bool ConfirmationResult { get; set; }

    public int ShowConfirmationCount { get; private set; }
    public string? LastTitle { get; private set; }
    public string? LastMessage { get; private set; }
    public string? LastConfirmText { get; private set; }
    public string? LastCancelText { get; private set; }

    public int ShowMessageCount { get; private set; }

    /// <summary>
    /// When set, both dialog methods await this before completing — lets tests
    /// hold a dialog "open" to exercise single-flight guards.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText, string cancelText)
    {
        ShowConfirmationCount++;
        LastTitle = title;
        LastMessage = message;
        LastConfirmText = confirmText;
        LastCancelText = cancelText;

        if (Gate is not null)
            await Gate.Task;

        return ConfirmationResult;
    }

    public async Task ShowMessageAsync(string title, string message, string buttonText)
    {
        ShowMessageCount++;
        LastTitle = title;
        LastMessage = message;
        LastConfirmText = buttonText;

        if (Gate is not null)
            await Gate.Task;
    }
}
