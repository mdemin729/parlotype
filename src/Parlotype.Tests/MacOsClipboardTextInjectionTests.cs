using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.TextInjection;
using Parlotype.Platform.TextInjection;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// The decisions around a macOS paste (ADR-077), with the pasteboard and the
/// keystroke replaced by fakes: a test must not overwrite the developer's
/// clipboard or type Cmd+V into whatever window is in front. macOS-only; every
/// test no-ops elsewhere, because the class is.
/// </summary>
public class MacOsClipboardTextInjectionTests
{
    private readonly FakePasteboard _pasteboard = new();
    private readonly FakePermissions _permissions = new();
    private readonly FakeTracker _tracker = new();
    private int _shortcutsPosted;
    private bool _shortcutIsAccepted = true;

    private Task Inject(string text)
    {
        // Guarded by the OperatingSystem.IsMacOS() early-return in each test;
        // the type is [SupportedOSPlatform("macos")].
        if (!OperatingSystem.IsMacOS())
            throw new InvalidOperationException("macOS-only");

        var service = new MacOsClipboardTextInjectionService(
            _tracker,
            _permissions,
            NullLogger<MacOsClipboardTextInjectionService>.Instance,
            () => _pasteboard.Text,
            _pasteboard.Set,
            _pasteboard.Clear,
            () =>
            {
                _shortcutsPosted++;
                return _shortcutIsAccepted;
            });

        return service.InjectTextAsync(text);
    }

    [Fact]
    public async Task WithAccessibility_PastesAndPutsTheOldClipboardBack()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        _pasteboard.Text = "what the user had copied";

        await Inject("dictated text");

        Assert.Equal(1, _shortcutsPosted);
        Assert.Equal(1, _tracker.Activations);
        Assert.Equal("what the user had copied", _pasteboard.Text);
        // The dictated text went on as transient; the restore did not.
        Assert.Equal([("dictated text", true), ("what the user had copied", false)], _pasteboard.Writes);
    }

    [Fact]
    public async Task WithAccessibility_AndAnEmptyClipboard_LeavesItEmptyAgain()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        await Inject("dictated text");

        Assert.Null(_pasteboard.Text);
        Assert.Equal(1, _pasteboard.Clears);
    }

    [Fact]
    public async Task WithoutAccessibility_LeavesTheTextOnTheClipboard_AndSaysSo()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // The case from the first real test of the port: dictation started
        // with the widget's button, recognized, and then silently went nowhere.
        _permissions.Accessibility = SystemPermissionState.Denied;
        _pasteboard.Text = "what the user had copied";

        await Assert.ThrowsAsync<TextInjectionNotPermittedException>(() => Inject("dictated text"));

        Assert.Equal("dictated text", _pasteboard.Text);
        // An ordinary copy, not a transient one: it is meant to be found.
        Assert.Equal([("dictated text", false)], _pasteboard.Writes);
        Assert.Equal(0, _shortcutsPosted);
        Assert.Equal(0, _tracker.Activations);
    }

    [Fact]
    public async Task WhenTheKeystrokeIsRefusedAnyway_TheTextIsNotRestoredAway()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        // The permission reads as granted but macOS drops the event, which a
        // stale grant after a rebuild does.
        _shortcutIsAccepted = false;
        _pasteboard.Text = "what the user had copied";

        await Assert.ThrowsAsync<TextInjectionNotPermittedException>(() => Inject("dictated text"));

        Assert.Equal("dictated text", _pasteboard.Text);
        Assert.Equal(("dictated text", false), _pasteboard.Writes[^1]);
    }

    [Fact]
    public async Task EmptyText_DoesNothing()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        _pasteboard.Text = "untouched";

        await Inject(string.Empty);

        Assert.Equal("untouched", _pasteboard.Text);
        Assert.Empty(_pasteboard.Writes);
        Assert.Equal(0, _shortcutsPosted);
    }

    private sealed class FakePasteboard
    {
        public string? Text { get; set; }
        public List<(string Text, bool Transient)> Writes { get; } = [];
        public int Clears { get; private set; }

        public void Set(string text, bool transient)
        {
            Text = text;
            Writes.Add((text, transient));
        }

        public void Clear()
        {
            Text = null;
            Clears++;
        }
    }

    private sealed class FakePermissions : ISystemPermissionService
    {
        public SystemPermissionState Accessibility { get; set; } = SystemPermissionState.Granted;

        public SystemPermissionState GetState(SystemPermission permission) =>
            permission == SystemPermission.Accessibility ? Accessibility : SystemPermissionState.Granted;

        public Task<SystemPermissionState> RequestAsync(
            SystemPermission permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetState(permission));

        public bool OpenSettings(SystemPermission permission) => true;
    }

    private sealed class FakeTracker : ITargetWindowTracker
    {
        public int Activations { get; private set; }
        public nint? TargetWindow => 42;

        public bool ActivateTargetWindow()
        {
            Activations++;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
