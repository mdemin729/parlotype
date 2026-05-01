using Parlotype.Core.TextInjection;

namespace Parlotype.Desktop.V2.Tests.Mocks;

public sealed class MockTextInjectionService : ITextInjectionService
{
    public List<string> InjectedTexts { get; } = [];

    /// <summary>When set, <see cref="InjectTextAsync"/> will throw this exception.</summary>
    public Exception? ThrowOnInject { get; set; }

    public Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (ThrowOnInject is not null)
            throw ThrowOnInject;

        InjectedTexts.Add(text);
        return Task.CompletedTask;
    }
}
