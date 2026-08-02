namespace Parlotype.Desktop.Services;

/// <summary>
/// Small OS-shell integrations that ViewModels need: the system clipboard and the
/// file manager.
/// </summary>
/// <remarks>
/// Behind an interface rather than called inline so ViewModels stay
/// headless-testable — a test that exercised these directly would need a real
/// <c>TopLevel</c> for the clipboard and would spawn actual Explorer windows on
/// the developer's machine.
/// </remarks>
public interface IShellService
{
    /// <summary>
    /// Puts <paramref name="text"/> on the system clipboard. Returns false when no
    /// clipboard is reachable (no window yet, or a headless host).
    /// </summary>
    Task<bool> CopyTextAsync(string text);

    /// <summary>
    /// Opens <paramref name="path"/> in the OS file manager, creating the directory
    /// first if it does not exist yet. Returns false if it could not be opened.
    /// </summary>
    Task<bool> OpenDirectoryAsync(string path);
}
