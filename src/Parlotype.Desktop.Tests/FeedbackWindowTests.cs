using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The feedback window itself (ADR-070): focus, limits typed by a real keyboard,
/// accessibility names, and the draft surviving a close.
/// </summary>
[Collection(CultureBoundTests.Name)]
public class FeedbackWindowTests : IDisposable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    public FeedbackWindowTests() => Localizer.Instance.SetCulture(English);

    public void Dispose() => Localizer.Instance.SetCulture(English);

    private static FeedbackViewModel NewViewModel() =>
        new(new MockFeedbackService(), new MockShellService(), new MockUserDialogService());

    private static FeedbackWindow Open(FeedbackViewModel vm)
    {
        var window = new FeedbackWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Find<T>(Window window, string name) where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} not found");

    [AvaloniaFact]
    public void OnOpen_TheMessageBoxHasFocus()
    {
        var window = Open(NewViewModel());

        Assert.True(Find<TextBox>(window, "MessageBox").IsFocused);
        window.Close();
    }

    [AvaloniaFact]
    public void MessageBox_StopsTypingAtFiveThousandCharacters()
    {
        var vm = NewViewModel();
        var window = Open(vm);
        var box = Find<TextBox>(window, "MessageBox");

        Assert.Equal(5000, box.MaxLength);
        box.Focus();
        window.KeyTextInput(new string('a', 4998));
        window.KeyTextInput("bcdef");

        Assert.Equal(5000, vm.Message.Length);
        Assert.EndsWith("bc", vm.Message, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task WhileSending_TheInputsCannotBeEdited()
    {
        var service = new MockFeedbackService { Gate = new TaskCompletionSource() };
        var vm = new FeedbackViewModel(service, new MockShellService(), new MockUserDialogService()) { Message = "hello" };
        var window = Open(vm);

        var send = vm.SendCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Find<TextBox>(window, "MessageBox").IsReadOnly);
        Assert.True(Find<TextBox>(window, "EmailBox").IsReadOnly);
        foreach (var name in new[] { "FeedbackKindButton", "BugKindButton", "IncludeAppVersionBox", "IncludeOsBox", "IncludeUiLanguageBox" })
            Assert.False(Find<Control>(window, name).IsEnabled, $"{name} is still enabled while sending");

        service.Result = new Parlotype.Core.Feedback.FeedbackSendResult(Parlotype.Core.Feedback.FeedbackSendOutcome.RateLimited, 429, "rate_limited");
        service.Gate.SetResult();
        await send;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Find<TextBox>(window, "MessageBox").IsReadOnly);
        Assert.True(Find<Control>(window, "IncludeOsBox").IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void EmailBox_IsCappedAtTheContractsLength()
    {
        var window = Open(NewViewModel());

        Assert.Equal(254, Find<TextBox>(window, "EmailBox").MaxLength);
        window.Close();
    }

    [AvaloniaFact]
    public void EveryInput_HasAnAccessibleName()
    {
        var window = Open(NewViewModel());

        foreach (var name in new[]
                 {
                     "FeedbackKindButton", "BugKindButton", "MessageBox", "EmailBox",
                     "IncludeAppVersionBox", "IncludeOsBox", "IncludeUiLanguageBox",
                     "CloseButton", "SendButton", "WebsiteButton", "GitHubButton",
                 })
        {
            var control = Find<Control>(window, name);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), $"{name} has no accessible name");
        }

        Assert.Equal(Strings.Feedback_MessageLabel, AutomationProperties.GetName(Find<TextBox>(window, "MessageBox")));
        Assert.Equal(Strings.Feedback_EmailLabel, AutomationProperties.GetName(Find<TextBox>(window, "EmailBox")));
        window.Close();
    }

    [AvaloniaFact]
    public void TheDetailCheckBoxes_ShowTheRealValues()
    {
        var window = Open(NewViewModel());

        Assert.Equal("App version: 0.6.0", Find<CheckBox>(window, "IncludeAppVersionBox").Content);
        Assert.Equal("Operating system: Windows 11 24H2 (10.0.26100)", Find<CheckBox>(window, "IncludeOsBox").Content);
        Assert.Equal("Interface language: en", Find<CheckBox>(window, "IncludeUiLanguageBox").Content);
        window.Close();
    }

    [AvaloniaFact]
    public void TheFallbackButtons_AreHiddenUntilNeeded()
    {
        var vm = NewViewModel();
        var window = Open(vm);
        var panel = Find<StackPanel>(window, "FallbackPanel");

        Assert.False(panel.IsVisible);
        vm.ShowFallback = true;
        Assert.True(panel.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ClosingWithUnsentText_KeepsItForTheNextWindow()
    {
        var vm = NewViewModel();
        var first = Open(vm);
        Find<TextBox>(first, "MessageBox").Focus();
        first.KeyTextInput("half-written");
        first.Close();

        var second = Open(vm);

        Assert.Equal("half-written", Find<TextBox>(second, "MessageBox").Text);
        second.Close();
    }

    [AvaloniaFact]
    public void Escape_ClosesTheWindow_AndKeepsTheDraft()
    {
        var vm = NewViewModel();
        vm.Message = "keep me";
        var window = Open(vm);
        var closed = false;
        window.Closed += (_, _) => closed = true;

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
        Assert.Equal("keep me", vm.Message);
    }

    [AvaloniaFact]
    public async Task ASuccessfulSend_ClosesTheWindow()
    {
        var vm = NewViewModel();
        vm.Message = "Works well.";
        var window = Open(vm);
        var closed = false;
        window.Closed += (_, _) => closed = true;

        await vm.SendCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    [AvaloniaFact]
    public async Task ARejectedEmail_MovesFocusToTheEmailBox()
    {
        var vm = NewViewModel();
        vm.Message = "Hello";
        vm.Email = "not an address";
        var window = Open(vm);

        await vm.SendCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Find<TextBox>(window, "EmailBox").IsFocused);
        Assert.True(Find<TextBlock>(window, "EmailInvalidText").IsVisible);
        window.Close();
    }
}
