namespace Parlotype.Desktop.Services;

/// <summary>
/// Shows simple modal dialogs from ViewModels without coupling them to
/// Avalonia window types (keeps ViewModels headless-testable via a mock).
/// </summary>
public interface IUserDialogService
{
    /// <summary>
    /// Shows a modal confirm/cancel dialog and returns <c>true</c> when the
    /// user clicked the confirm button, <c>false</c> when they cancelled or
    /// closed the dialog. Safe to call from any thread.
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText, string cancelText);

    /// <summary>
    /// Shows a modal message with a single dismiss button. Completes when the
    /// dialog is closed. Safe to call from any thread.
    /// </summary>
    Task ShowMessageAsync(string title, string message, string buttonText);
}
