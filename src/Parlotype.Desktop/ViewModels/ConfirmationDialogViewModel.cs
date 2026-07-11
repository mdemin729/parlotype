namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Immutable content for <see cref="Views.ConfirmationDialog"/> — a title, a
/// wrapped message, and the two button captions. The dialog's result (confirm
/// vs cancel/close) is returned through <c>ShowDialog&lt;bool?&gt;</c>, so no
/// observable state is needed here.
/// </summary>
public sealed class ConfirmationDialogViewModel
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    /// <summary>False for message-only dialogs (no cancel caption) — hides the cancel button.</summary>
    public bool HasCancel => CancelText.Length > 0;

    public ConfirmationDialogViewModel(string title, string message, string confirmText, string? cancelText)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText ?? string.Empty;
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public ConfirmationDialogViewModel()
        : this("Title", "Message describing the problem and what the user can do about it.", "OK", "Cancel")
    {
    }
}
