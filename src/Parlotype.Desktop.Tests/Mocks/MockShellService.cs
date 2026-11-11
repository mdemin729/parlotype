using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Records clipboard, file-manager and browser requests instead of performing them,
/// so tests never touch the real clipboard or spawn Explorer or browser windows.
/// </summary>
public sealed class MockShellService : IShellService
{
    /// <summary>What both operations should report. Defaults to success.</summary>
    public bool Result { get; set; } = true;

    public int CopyTextCount { get; private set; }
    public string? LastCopiedText { get; private set; }

    public int OpenDirectoryCount { get; private set; }
    public string? LastOpenedPath { get; private set; }

    public int OpenUrlCount { get; private set; }
    public Uri? LastOpenedUrl { get; private set; }

    /// <summary>What <see cref="OpenUrlAsync"/> reports. Defaults to success.</summary>
    public bool OpenUrlResult { get; set; } = true;

    public Task<bool> OpenUrlAsync(Uri url)
    {
        OpenUrlCount++;
        LastOpenedUrl = url;
        return Task.FromResult(OpenUrlResult);
    }

    public Task<bool> CopyTextAsync(string text)
    {
        CopyTextCount++;
        LastCopiedText = text;
        return Task.FromResult(Result);
    }

    public Task<bool> OpenDirectoryAsync(string path)
    {
        OpenDirectoryCount++;
        LastOpenedPath = path;
        return Task.FromResult(Result);
    }
}
