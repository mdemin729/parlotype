using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Onboarding;
using Parlotype.Desktop.ViewModels.Onboarding;
using Parlotype.Desktop.Views;

namespace Parlotype.Desktop.Services;

/// <summary>
/// <see cref="IOnboardingService"/> over the real wizard window (ADR-056).
/// The auto-show flag is written <em>before</em> the window opens so a crash
/// mid-tour still counts as offered; the tour remains reachable from
/// Settings → Help. Window lifetime mirrors <see cref="WindowManager"/>:
/// one cached instance, recreated when its platform handle is gone.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly ILogger<OnboardingService>? _logger;
    private OnboardingWindow? _window;

    public OnboardingService(
        IServiceProvider services,
        ISettingsService settings,
        ILogger<OnboardingService>? logger = null)
    {
        _services = services;
        _settings = settings;
        _logger = logger;
    }

    public async Task MaybeShowOnFirstRunAsync()
    {
        try
        {
            if (!await ShouldAutoShowAsync())
                return;

            await _settings.SetAsync(SettingsKeys.OnboardingCompleted, "True");
            ShowWizard();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "First-run onboarding check failed; skipping the tour");
        }
    }

    /// <summary>
    /// House default-off convention: unset or unparsable means "never offered".
    /// </summary>
    private async Task<bool> ShouldAutoShowAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.OnboardingCompleted);
        return !(bool.TryParse(saved, out var done) && done);
    }

    public void ShowWizard() => ShowWizardCore();

    /// <summary>Seam for trigger tests; the real path marshals to the UI thread.</summary>
    protected virtual void ShowWizardCore() => Dispatcher.UIThread.Post(() =>
    {
        if (_window is null || _window.PlatformImpl is null)
        {
            var viewModel = _services.GetRequiredService<OnboardingWizardViewModel>();
            var window = new OnboardingWindow
            {
                DataContext = viewModel,
                HighlightService = _services.GetRequiredService<OnboardingHighlightService>(),
            };

            void OnCloseRequested(object? sender, EventArgs e) => window.Close();
            viewModel.CloseRequested += OnCloseRequested;
            window.Closed += (_, _) =>
            {
                viewModel.CloseRequested -= OnCloseRequested;
                if (ReferenceEquals(_window, window))
                    _window = null;
            };

            _window = window;
        }

        if (_window.DataContext is OnboardingWizardViewModel vm)
            vm.Start();

        _window.Show();
        _window.Activate();
    });
}
