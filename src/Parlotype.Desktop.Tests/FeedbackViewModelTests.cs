using System.Globalization;
using Avalonia.Headless.XUnit;
using Parlotype.Core.Feedback;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The feedback window's behaviour (ADR-070), one test per row of the response table as
/// the dialog sees it, against <see cref="MockFeedbackService"/> — never parlotype.dev.
/// </summary>
/// <remarks>
/// In the culture-bound collection: several tests switch the interface language, and the
/// rest assert on English copy.
/// </remarks>
[Collection(CultureBoundTests.Name)]
public class FeedbackViewModelTests : IDisposable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de");

    private readonly MockFeedbackService _service = new();
    private readonly MockShellService _shell = new();
    private readonly MockUserDialogService _dialogs = new();

    public FeedbackViewModelTests() => Localizer.Instance.SetCulture(English);

    public void Dispose() => Localizer.Instance.SetCulture(English);

    private FeedbackViewModel NewViewModel(string message = "The hotkey stops working after sleep.")
    {
        var vm = new FeedbackViewModel(_service, _shell, _dialogs) { Message = message };
        return vm;
    }

    private static FeedbackSendResult Result(FeedbackSendOutcome outcome, int? status = null, string? error = null, string? field = null) =>
        new(outcome, status, error, field);

    // ------------------------------------------------------------ Send button

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Send_IsDisabled_WithoutText(string message)
    {
        var vm = NewViewModel(message);

        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Send_IsEnabled_WithText()
    {
        var vm = NewViewModel();

        Assert.True(vm.SendCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task WhileARequestIsInFlight_SendIsDisabled_AndASecondPressSendsNothing()
    {
        _service.Gate = new TaskCompletionSource();
        var vm = NewViewModel();

        var first = vm.SendCommand.ExecuteAsync(null);

        Assert.True(vm.IsSending);
        Assert.False(vm.SendCommand.CanExecute(null));
        Assert.Equal(Strings.Feedback_Status_Sending, vm.StatusText);

        await vm.SendCommand.ExecuteAsync(null);
        Assert.Single(_service.Submissions);

        _service.Gate.SetResult();
        await first;

        Assert.False(vm.IsSending);
    }

    [AvaloniaFact]
    public void Limits_AreTheContracts()
    {
        var vm = NewViewModel();

        Assert.Equal(5000, vm.MessageMaxLength);
        Assert.Equal(254, vm.EmailMaxLength);
    }

    [AvaloniaFact]
    public void AMessageOverTheLimit_CannotBeSent()
    {
        var vm = NewViewModel(new string('a', FeedbackLimits.MessageMaxLength + 1));

        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CharacterCount_FollowsTheMessage()
    {
        var vm = NewViewModel("hello");

        Assert.Equal("5 / 5000", vm.CharacterCountText);
    }

    // ------------------------------------------------------------ what is sent

    [AvaloniaFact]
    public async Task Submission_CarriesTheShownValues()
    {
        var vm = NewViewModel("  It crashed.\r\n");
        vm.IsBugReport = true;
        vm.Email = " someone@example.com ";

        await vm.SendCommand.ExecuteAsync(null);

        var sent = Assert.Single(_service.Submissions);
        Assert.Equal(FeedbackKind.Bug, sent.Kind);
        Assert.Equal("It crashed.", sent.Message);
        Assert.Equal("someone@example.com", sent.Email);
        Assert.Equal(vm.AppVersion, sent.AppVersion);
        Assert.Equal(vm.OsDescription, sent.OperatingSystem);
        Assert.Equal("en", sent.UiLanguage);
    }

    [AvaloniaFact]
    public async Task UntickedDetails_AreLeftOut()
    {
        var vm = NewViewModel();
        vm.IncludeAppVersion = false;
        vm.IncludeOperatingSystem = false;
        vm.IncludeUiLanguage = false;

        await vm.SendCommand.ExecuteAsync(null);

        var sent = Assert.Single(_service.Submissions);
        Assert.Null(sent.AppVersion);
        Assert.Null(sent.OperatingSystem);
        Assert.Null(sent.UiLanguage);
        Assert.Null(sent.Email);
    }

    [AvaloniaFact]
    public async Task EachDetail_CanBeUntickedOnItsOwn()
    {
        var vm = NewViewModel();
        vm.IncludeOperatingSystem = false;

        await vm.SendCommand.ExecuteAsync(null);

        var sent = Assert.Single(_service.Submissions);
        Assert.NotNull(sent.AppVersion);
        Assert.Null(sent.OperatingSystem);
        Assert.NotNull(sent.UiLanguage);
    }

    [AvaloniaFact]
    public void TheDetailLabels_ShowTheRealValues()
    {
        _service.Diagnostics = new FeedbackDiagnostics("0.6.0-beta.1", "Windows 10 22H2 (10.0.19045)");
        var vm = NewViewModel();

        Assert.Equal("App version: 0.6.0-beta.1", vm.AppVersionLabel);
        Assert.Equal("Operating system: Windows 10 22H2 (10.0.19045)", vm.OperatingSystemLabel);
        Assert.Equal("Interface language: en", vm.UiLanguageLabel);
    }

    [AvaloniaFact]
    public void Kind_IsAPairOfRadioButtons()
    {
        var vm = NewViewModel();
        Assert.True(vm.IsFeedback);

        vm.IsBugReport = true;
        Assert.False(vm.IsFeedback);

        vm.IsFeedback = true;
        Assert.False(vm.IsBugReport);
    }

    // ------------------------------------------------------------ the response table

    [AvaloniaTheory]
    [InlineData(201, false)]
    [InlineData(200, true)]
    public async Task Sent_ThanksTheUser_ClearsTheDraft_AndCloses(int status, bool duplicate)
    {
        _service.Result = new FeedbackSendResult(FeedbackSendOutcome.Sent, status, SubmissionId: 42, IsDuplicate: duplicate);
        var vm = NewViewModel();
        vm.Email = "someone@example.com";
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogs.ShowMessageCount);
        Assert.Equal(Strings.Feedback_Sent_Title, _dialogs.LastTitle);
        Assert.Contains("#42", _dialogs.LastMessage, StringComparison.Ordinal);
        Assert.Equal(1, closed);
        Assert.Equal("", vm.Message);
        Assert.Equal("", vm.Email);
        Assert.False(vm.ShowFallback);
        Assert.Null(vm.StatusText);
    }

    [AvaloniaFact]
    public async Task InvalidEmailFromTheServer_HighlightsTheField_AndKeepsTheText()
    {
        _service.Result = Result(FeedbackSendOutcome.InvalidEmail, 400, "invalid_field", "email");
        var vm = NewViewModel("keep me");
        vm.Email = "someone@example.com";

        await vm.SendCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmailInvalid);
        Assert.False(vm.ShowFallback);
        Assert.Equal("keep me", vm.Message);
    }

    [AvaloniaFact]
    public async Task AnAddressThatCannotPass_IsCaughtBeforeAnyRequest()
    {
        var vm = NewViewModel();
        vm.Email = "not an address";

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Empty(_service.Submissions);
        Assert.True(vm.IsEmailInvalid);
    }

    [AvaloniaFact]
    public async Task EditingTheEmail_ClearsTheHighlight()
    {
        var vm = NewViewModel();
        vm.Email = "nope";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmailInvalid);

        vm.Email = "nope@example.com";

        Assert.False(vm.IsEmailInvalid);
    }

    [AvaloniaFact]
    public async Task RateLimited_AsksToWaitAMinute_KeepsTheText_AndOffersNoFallback()
    {
        _service.Result = Result(FeedbackSendOutcome.RateLimited, 429, "rate_limited");
        var vm = NewViewModel("keep me");
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Feedback_Status_RateLimited, vm.StatusText);
        Assert.True(vm.IsStatusError);
        Assert.False(vm.ShowFallback);
        Assert.Equal("keep me", vm.Message);
        Assert.False(closed);
        Assert.True(vm.SendCommand.CanExecute(null));
    }

    public static TheoryData<FeedbackSendOutcome, int?, string?, string> FallbackRows => new()
    {
        // 400 other than email, 405, 413, 415: a bug in the app — generic error.
        { FeedbackSendOutcome.Rejected, 400, "invalid_json", nameof(Strings.Feedback_Status_Rejected) },
        { FeedbackSendOutcome.Rejected, 405, "method_not_allowed", nameof(Strings.Feedback_Status_Rejected) },
        { FeedbackSendOutcome.Rejected, 413, "payload_too_large", nameof(Strings.Feedback_Status_Rejected) },
        { FeedbackSendOutcome.Rejected, 415, "unsupported_media_type", nameof(Strings.Feedback_Status_Rejected) },
        // The app path is closed: hand over to the website.
        { FeedbackSendOutcome.AppPathClosed, 429, "daily_limit", nameof(Strings.Feedback_Status_AppPathClosed) },
        { FeedbackSendOutcome.AppPathClosed, 503, "app_paused", nameof(Strings.Feedback_Status_AppPathClosed) },
        // 500, anything else, timeout, no network.
        { FeedbackSendOutcome.ServerError, 500, "storage_failed", nameof(Strings.Feedback_Status_ServerError) },
        { FeedbackSendOutcome.ServerError, 502, null, nameof(Strings.Feedback_Status_ServerError) },
        { FeedbackSendOutcome.Timeout, null, null, nameof(Strings.Feedback_Status_Offline) },
        { FeedbackSendOutcome.NetworkError, null, null, nameof(Strings.Feedback_Status_Offline) },
    };

    [AvaloniaTheory]
    [MemberData(nameof(FallbackRows))]
    public async Task WhenTheAppCannotDeliver_TheTextStays_AndTheFallbackIsOffered(
        FeedbackSendOutcome outcome, int? status, string? error, string expectedKey)
    {
        _service.Result = Result(outcome, status, error);
        var vm = NewViewModel("keep me");
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(Localizer.Lookup(expectedKey), vm.StatusText);
        Assert.True(vm.IsStatusError);
        Assert.True(vm.ShowFallback);
        Assert.Equal("keep me", vm.Message);
        Assert.False(closed);
        Assert.Equal(0, _dialogs.ShowMessageCount);
    }

    [AvaloniaFact]
    public async Task AServiceThatThrows_StillKeepsTheTextAndOffersTheFallback()
    {
        var vm = new FeedbackViewModel(new ThrowingFeedbackService(), _shell, _dialogs) { Message = "keep me" };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.True(vm.ShowFallback);
        Assert.Equal("keep me", vm.Message);
        Assert.False(vm.IsSending);
    }

    // ------------------------------------------------------------ fallback

    [AvaloniaFact]
    public async Task SendFromWebsite_CopiesTheMessage_AndOpensTheEnglishForm()
    {
        var vm = NewViewModel("  line one\r\nline two  ");

        await vm.SendFromWebsiteCommand.ExecuteAsync(null);

        Assert.Equal("line one\nline two", _shell.LastCopiedText);
        Assert.Equal(new Uri("https://parlotype.dev/feedback/"), _shell.LastOpenedUrl);
        Assert.Equal(Strings.Feedback_Status_Copied, vm.StatusText);
        Assert.False(vm.IsStatusError);
    }

    [AvaloniaFact]
    public async Task SendFromWebsite_OpensTheFormInTheInterfaceLanguage_WithNothingInTheUrl()
    {
        Localizer.Instance.SetCulture(German);
        var vm = NewViewModel("secret words");
        vm.Email = "who@example.com";

        await vm.SendFromWebsiteCommand.ExecuteAsync(null);

        var url = _shell.LastOpenedUrl!;
        Assert.Equal("https://parlotype.dev/de/feedback/", url.AbsoluteUri);
        Assert.Empty(url.Query);
        Assert.DoesNotContain("secret", url.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("who", url.AbsoluteUri, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task GitHubIssues_IsTheSecondWayOut()
    {
        var vm = NewViewModel("keep me");

        await vm.OpenGitHubIssuesCommand.ExecuteAsync(null);

        Assert.Equal(FeedbackLinks.GitHubIssues, _shell.LastOpenedUrl);
        Assert.Equal("keep me", _shell.LastCopiedText);
        Assert.Equal("keep me", vm.Message);
    }

    [AvaloniaFact]
    public async Task ACopyThatFails_IsReported()
    {
        _shell.Result = false;
        var vm = NewViewModel();

        await vm.SendFromWebsiteCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Feedback_Status_CopyFailed, vm.StatusText);
        Assert.True(vm.IsStatusError);
    }

    [AvaloniaFact]
    public async Task ABrowserThatWillNotOpen_IsReportedWithTheAddress()
    {
        _shell.OpenUrlResult = false;
        var vm = NewViewModel();

        await vm.SendFromWebsiteCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Format_Feedback_Status_BrowserFailedFormat("https://parlotype.dev/feedback/"), vm.StatusText);
    }

    [AvaloniaFact]
    public async Task TheFallback_NeverSendsAnything()
    {
        var vm = NewViewModel();

        await vm.SendFromWebsiteCommand.ExecuteAsync(null);
        await vm.OpenGitHubIssuesCommand.ExecuteAsync(null);

        Assert.Empty(_service.Submissions);
    }

    // ------------------------------------------------------------ draft

    [AvaloniaFact]
    public void Close_KeepsTheDraft()
    {
        var vm = NewViewModel("half-written");
        vm.Email = "someone@example.com";
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.CloseCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal("half-written", vm.Message);
        Assert.Equal("someone@example.com", vm.Email);
        Assert.Empty(_service.Submissions);
    }

    // ------------------------------------------------------------ interface language

    [AvaloniaFact]
    public void ALanguageSwitch_ReRendersEveryComposedString()
    {
        var vm = NewViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(German);

        Assert.Contains(nameof(FeedbackViewModel.AppVersionLabel), raised);
        Assert.Contains(nameof(FeedbackViewModel.OperatingSystemLabel), raised);
        Assert.Contains(nameof(FeedbackViewModel.UiLanguageLabel), raised);
        Assert.Contains(nameof(FeedbackViewModel.UiLanguage), raised);
        Assert.NotEqual("App version: 0.6.0", vm.AppVersionLabel);
        Assert.Contains("0.6.0", vm.AppVersionLabel, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ALanguageSwitch_ChangesTheLanguageThatWillBeSent()
    {
        var vm = NewViewModel();
        Assert.Equal("en", vm.UiLanguage);

        Localizer.Instance.SetCulture(German);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("de", vm.UiLanguage);
        Assert.Equal("de", Assert.Single(_service.Submissions).UiLanguage);
        Assert.EndsWith(": de", vm.UiLanguageLabel, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ALanguageSwitch_TranslatesTheStatusLineInPlace()
    {
        _service.Result = Result(FeedbackSendOutcome.RateLimited, 429, "rate_limited");
        var vm = NewViewModel();
        await vm.SendCommand.ExecuteAsync(null);
        var english = vm.StatusText;

        Localizer.Instance.SetCulture(German);

        Assert.NotNull(vm.StatusText);
        Assert.NotEqual(english, vm.StatusText);
        Assert.Equal(Strings.Feedback_Status_RateLimited, vm.StatusText);
    }

    // ------------------------------------------------------------ entry points

    [AvaloniaFact]
    public void TrayMenu_OpensTheFeedbackWindow()
    {
        var windows = new MockWindowManager();
        var app = new AppViewModel(windows);

        app.OpenFeedbackCommand.Execute(null);

        Assert.Equal(1, windows.ShowFeedbackCount);
        Assert.Equal(Strings.Tray_SendFeedback, app.TraySendFeedbackLabel);
    }

    [AvaloniaFact]
    public void HelpPage_OpensTheFeedbackWindow()
    {
        var windows = new MockWindowManager();
        var help = new HelpSettingsViewModel(new MockOnboardingService(), windowManager: windows);

        help.SendFeedbackCommand.Execute(null);

        Assert.Equal(1, windows.ShowFeedbackCount);
    }

    [AvaloniaFact]
    public void TrayLabel_FollowsTheInterfaceLanguage()
    {
        var app = new AppViewModel(new MockWindowManager());
        var raised = new List<string?>();
        app.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(German);

        Assert.Contains(nameof(AppViewModel.TraySendFeedbackLabel), raised);
        Assert.NotEqual("Send feedback…", app.TraySendFeedbackLabel);
    }

    private sealed class ThrowingFeedbackService : IFeedbackService
    {
        public FeedbackDiagnostics Diagnostics { get; } = new("0.6.0", "Windows 11");

        public Task<FeedbackSendResult> SendAsync(FeedbackSubmission submission, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
