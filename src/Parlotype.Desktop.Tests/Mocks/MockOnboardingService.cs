using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockOnboardingService : IOnboardingService
{
    public int MaybeShowCount { get; private set; }
    public int ShowWizardCount { get; private set; }

    public Task MaybeShowOnFirstRunAsync()
    {
        MaybeShowCount++;
        return Task.CompletedTask;
    }

    public void ShowWizard() => ShowWizardCount++;
}
