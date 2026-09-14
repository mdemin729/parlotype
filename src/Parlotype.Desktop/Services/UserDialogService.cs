using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;

namespace Parlotype.Desktop.Services;

/// <summary>
/// <see cref="IUserDialogService"/> implementation backed by
/// <see cref="ConfirmationDialog"/>. Marshals to the UI thread and uses a
/// visible owner when available, otherwise showing a standalone dialog.
/// </summary>
public sealed class UserDialogService : IUserDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText, string cancelText)
    {
        // Must show dialog on UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(
                () => ShowConfirmationCoreAsync(title, message, confirmText, cancelText));
        }

        return await ShowConfirmationCoreAsync(title, message, confirmText, cancelText);
    }

    public async Task ShowMessageAsync(string title, string message, string buttonText)
    {
        // Message-only variant: same dialog with the cancel button hidden
        // (empty cancel caption); the result is irrelevant.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ShowConfirmationCoreAsync(title, message, buttonText, cancelText: null));
            return;
        }

        await ShowConfirmationCoreAsync(title, message, buttonText, cancelText: null);
    }

    private static async Task<bool> ShowConfirmationCoreAsync(string title, string message, string confirmText, string? cancelText)
    {
        var dialog = new ConfirmationDialog
        {
            DataContext = new ConfirmationDialogViewModel(title, message, confirmText, cancelText),
        };

        var owner = GetOwnerWindow();
        var result = owner is not null
            ? await dialog.ShowDialog<bool?>(owner)
            : await dialog.ShowStandaloneAsync();

        // Closing the window without choosing counts as a cancel.
        return result == true;
    }

    private static Window? GetOwnerWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        // A hidden tray window is not a valid modal owner. In that case the
        // dialog stands alone rather than failing to report the original error.
        foreach (var window in desktop.Windows)
        {
            if (window.IsVisible)
                return window;
        }

        return null;
    }
}
