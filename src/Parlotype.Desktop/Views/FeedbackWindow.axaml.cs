using System.ComponentModel;
using Avalonia.Controls;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views;

/// <summary>
/// The "Send feedback" window (ADR-070). Holds no state: the draft lives in the
/// singleton <see cref="FeedbackViewModel"/>, and <see cref="Services.WindowManager"/>
/// builds a new window on each open. Code-behind only moves focus and closes on request.
/// </summary>
public partial class FeedbackWindow : Window
{
    private FeedbackViewModel? _viewModel;

    public FeedbackWindow()
    {
        InitializeComponent();

        // Typing is the only thing to do here, so the caret starts in the message.
        Opened += (_, _) => this.FindControl<TextBox>("MessageBox")?.Focus();
        Closed += (_, _) => Attach(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Attach(DataContext as FeedbackViewModel);
    }

    private void Attach(FeedbackViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A rejected address is something only the user can fix: take them to it.
        if (e.PropertyName == nameof(FeedbackViewModel.IsEmailInvalid) && _viewModel?.IsEmailInvalid == true)
            this.FindControl<TextBox>("EmailBox")?.Focus();
    }
}
