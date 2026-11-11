using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Feedback;
using Parlotype.Core.Localization;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// The "Send feedback" window (ADR-070): kind, message, optional email, and the three
/// diagnostics the user can untick — sent once, only when they press Send.
/// </summary>
/// <remarks>
/// <para>
/// A DI singleton while the window is rebuilt on every open, so the draft outlives the
/// window: closing it with unsent text loses nothing until the app quits. Only a
/// confirmed send clears it.
/// </para>
/// <para>
/// The status line is composed in C#, so it is kept as a <see cref="FeedbackStatus"/> and
/// re-rendered when the interface language changes (ADR-064's live-switch rule). The
/// language diagnostic itself changes with it — it is a fact about the interface, so a
/// switch mid-draft changes what will be sent, and the window shows that.
/// </para>
/// </remarks>
public sealed partial class FeedbackViewModel : ViewModelBase
{
    private readonly IFeedbackService _feedback;
    private readonly IShellService _shell;
    private readonly IUserDialogService _dialogs;
    private readonly ILogger<FeedbackViewModel> _logger;

    private FeedbackStatus _status;
    private Uri? _unopenedUrl;

    public FeedbackViewModel(
        IFeedbackService feedback,
        IShellService shell,
        IUserDialogService dialogs,
        ILogger<FeedbackViewModel>? logger = null)
    {
        _feedback = feedback;
        _shell = shell;
        _dialogs = dialogs;
        _logger = logger ?? NullLogger<FeedbackViewModel>.Instance;

        Localizer.Instance.CultureChanged += (_, _) => OnCultureChanged();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public FeedbackViewModel()
        : this(new DesignFeedbackService(), new DesignShellService(), new DesignDialogService())
    {
    }

    /// <summary>Raised when the window should close — after a confirmed send, or Close.</summary>
    public event EventHandler? CloseRequested;

    public int MessageMaxLength => FeedbackLimits.MessageMaxLength;

    public int EmailMaxLength => FeedbackLimits.EmailMaxLength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFeedback))]
    private bool _isBugReport;

    /// <summary>The other radio button of the pair; the two always disagree.</summary>
    public bool IsFeedback
    {
        get => !IsBugReport;
        set => IsBugReport = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCountText))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _message = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private bool _isEmailInvalid;

    [ObservableProperty]
    private bool _includeAppVersion = true;

    [ObservableProperty]
    private bool _includeOperatingSystem = true;

    [ObservableProperty]
    private bool _includeUiLanguage = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    private bool _isSending;

    /// <summary>
    /// False while a request is in flight: what the user sees must stay what was sent,
    /// so the form is locked until the answer arrives.
    /// </summary>
    public bool IsEditable => !IsSending;

    /// <summary>True once the app could not deliver the message and the user should be offered another way.</summary>
    [ObservableProperty]
    private bool _showFallback;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _isStatusError;

    public string AppVersion => _feedback.Diagnostics.AppVersion;

    public string OsDescription => _feedback.Diagnostics.OperatingSystem;

    /// <summary>
    /// The interface language as a neutral code (<c>de</c>, <c>pt</c>) — the value sent
    /// as <c>uiLanguage</c> and the prefix of the website form's address.
    /// </summary>
    public string UiLanguage =>
        SupportedUiLanguages.MatchSystemCulture(Localizer.Instance.CurrentCulture.Name)?.CultureName
        ?? SupportedUiLanguages.NeutralCultureName;

    public string AppVersionLabel => Strings.Format_Feedback_Detail_AppVersionFormat(AppVersion);

    public string OperatingSystemLabel => Strings.Format_Feedback_Detail_OperatingSystemFormat(OsDescription);

    public string UiLanguageLabel => Strings.Format_Feedback_Detail_UiLanguageFormat(UiLanguage);

    public string CharacterCountText =>
        Strings.Format_Feedback_CharacterCountFormat(Message.Length, FeedbackLimits.MessageMaxLength);

    /// <summary>What a press of Send would put on the wire right now.</summary>
    public FeedbackSubmission BuildSubmission() => new(
        IsBugReport ? FeedbackKind.Bug : FeedbackKind.Feedback,
        FeedbackLimits.NormalizeMessage(Message),
        string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
        IncludeAppVersion ? AppVersion : null,
        IncludeOperatingSystem ? OsDescription : null,
        IncludeUiLanguage ? UiLanguage : null);

    private bool CanSend() => !IsSending && FeedbackLimits.IsSendableMessage(Message);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        // One request per press, and never a second while the first is in flight —
        // the command's CanExecute covers the button, this covers every other caller.
        if (!CanSend())
            return;

        if (!FeedbackLimits.IsAcceptableEmail(Email))
        {
            // Same rule as the server's: no point spending a request to learn this.
            IsEmailInvalid = true;
            SetStatus(FeedbackStatus.None);
            return;
        }

        IsEmailInvalid = false;
        IsSending = true;
        SetStatus(FeedbackStatus.Sending);

        // The draft as it was when Send was pressed. The view locks the fields while
        // the request is in flight; this covers any other writer, so a success never
        // wipes text that was not part of what the server stored.
        var sentMessage = Message;
        var sentEmail = Email;

        FeedbackSendResult result;
        try
        {
            result = await _feedback.SendAsync(BuildSubmission());
        }
        catch (Exception ex)
        {
            // The service turns every failure into a result; this is the belt to that
            // brace, so a bug there still leaves the user's text and a way out.
            _logger.LogError(ex, "Feedback send threw");
            result = new FeedbackSendResult(FeedbackSendOutcome.Rejected);
        }
        finally
        {
            IsSending = false;
        }

        switch (result.Outcome)
        {
            case FeedbackSendOutcome.Sent:
                SetStatus(FeedbackStatus.None);
                ShowFallback = false;
                await _dialogs.ShowMessageAsync(
                    Strings.Feedback_Sent_Title,
                    Strings.Format_Feedback_Sent_MessageFormat(result.SubmissionId?.ToString(CultureInfo.CurrentCulture) ?? "—"),
                    Strings.Common_Ok);
                if (Message != sentMessage || Email != sentEmail)
                {
                    // Edited after Send: that text is newer than what was stored.
                    // Keep it, and keep the window open for it.
                    return;
                }

                ClearDraft();
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return;

            case FeedbackSendOutcome.InvalidEmail:
                IsEmailInvalid = true;
                SetStatus(FeedbackStatus.None);
                return;

            case FeedbackSendOutcome.RateLimited:
                SetStatus(FeedbackStatus.RateLimited);
                return;

            case FeedbackSendOutcome.Rejected:
                SetStatus(FeedbackStatus.Rejected);
                break;

            case FeedbackSendOutcome.AppPathClosed:
                SetStatus(FeedbackStatus.AppPathClosed);
                break;

            case FeedbackSendOutcome.ServerError:
                SetStatus(FeedbackStatus.ServerError);
                break;

            default:
                SetStatus(FeedbackStatus.Offline);
                break;
        }

        ShowFallback = result.OffersFallback;
    }

    /// <summary>
    /// The website hand-over: copy the text, open the form in the interface language, and
    /// say what happened. Nothing goes into the URL.
    /// </summary>
    [RelayCommand]
    private Task SendFromWebsiteAsync() => HandOverAsync(FeedbackLinks.WebsiteForm(UiLanguage));

    [RelayCommand]
    private Task OpenGitHubIssuesAsync() => HandOverAsync(FeedbackLinks.GitHubIssues);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    partial void OnEmailChanged(string value) => IsEmailInvalid = false;

    private async Task HandOverAsync(Uri destination)
    {
        var copied = await _shell.CopyTextAsync(FeedbackLimits.NormalizeMessage(Message));
        var opened = await _shell.OpenUrlAsync(destination);

        _unopenedUrl = opened ? null : destination;
        SetStatus(!opened ? FeedbackStatus.BrowserFailed
            : copied ? FeedbackStatus.Copied
            : FeedbackStatus.CopyFailed);
    }

    private void ClearDraft()
    {
        Message = "";
        Email = "";
        IsBugReport = false;
        IsEmailInvalid = false;
        ShowFallback = false;
        SetStatus(FeedbackStatus.None);
    }

    private void SetStatus(FeedbackStatus status)
    {
        _status = status;
        StatusText = Describe(status);
        IsStatusError = status is not (FeedbackStatus.None or FeedbackStatus.Sending or FeedbackStatus.Copied);
    }

    private string? Describe(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Sending => Strings.Feedback_Status_Sending,
        FeedbackStatus.RateLimited => Strings.Feedback_Status_RateLimited,
        FeedbackStatus.Rejected => Strings.Feedback_Status_Rejected,
        FeedbackStatus.AppPathClosed => Strings.Feedback_Status_AppPathClosed,
        FeedbackStatus.ServerError => Strings.Feedback_Status_ServerError,
        FeedbackStatus.Offline => Strings.Feedback_Status_Offline,
        FeedbackStatus.Copied => Strings.Feedback_Status_Copied,
        FeedbackStatus.CopyFailed => Strings.Feedback_Status_CopyFailed,
        FeedbackStatus.BrowserFailed => Strings.Format_Feedback_Status_BrowserFailedFormat(_unopenedUrl?.AbsoluteUri ?? ""),
        _ => null,
    };

    /// <summary>
    /// Everything composed in C#. The diagnostic labels are re-raised as well as the
    /// status: <see cref="UiLanguage"/> is itself a function of the culture.
    /// </summary>
    private void OnCultureChanged()
    {
        OnPropertyChanged(nameof(UiLanguage));
        OnPropertyChanged(nameof(AppVersionLabel));
        OnPropertyChanged(nameof(OperatingSystemLabel));
        OnPropertyChanged(nameof(UiLanguageLabel));
        OnPropertyChanged(nameof(CharacterCountText));
        StatusText = Describe(_status);
    }

    /// <summary>The retained input the status line is rendered from.</summary>
    private enum FeedbackStatus
    {
        None,
        Sending,
        RateLimited,
        Rejected,
        AppPathClosed,
        ServerError,
        Offline,
        Copied,
        CopyFailed,
        BrowserFailed,
    }

    private sealed class DesignFeedbackService : IFeedbackService
    {
        public FeedbackDiagnostics Diagnostics { get; } = new("0.6.0", "Windows 11 24H2 (10.0.26100)");

        public Task<FeedbackSendResult> SendAsync(FeedbackSubmission submission, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FeedbackSendResult(FeedbackSendOutcome.Sent, 201, SubmissionId: 1));
    }

    private sealed class DesignShellService : IShellService
    {
        public Task<bool> CopyTextAsync(string text) => Task.FromResult(true);
        public Task<bool> OpenDirectoryAsync(string path) => Task.FromResult(true);
        public Task<bool> OpenUrlAsync(Uri url) => Task.FromResult(true);
    }

    private sealed class DesignDialogService : IUserDialogService
    {
        public Task<bool> ShowConfirmationAsync(string title, string message, string confirmText, string cancelText) =>
            Task.FromResult(false);

        public Task ShowMessageAsync(string title, string message, string buttonText) => Task.CompletedTask;
    }
}
