using Parlotype.Core.TextInjection;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockTextInjectionService : ITextInjectionService
{
    public List<string> InjectedTexts { get; } = [];

    /// <summary>When set, <see cref="InjectTextAsync"/> will throw this exception.</summary>
    public Exception? ThrowOnInject { get; set; }

    /// <summary>
    /// When set, <see cref="InjectTextAsync"/> awaits this before completing —
    /// lets a test hold a "paste" in flight and release it by hand instead of
    /// racing a real delay (ADR-068's <c>IsDictationBusy</c> regression guard:
    /// the widget must not auto-hide while a transcription is still being typed).
    /// Left null (the default) every existing caller completes immediately, exactly
    /// as before this was added.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task InjectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Gate is not null)
            await Gate.Task;

        if (ThrowOnInject is not null)
            throw ThrowOnInject;

        InjectedTexts.Add(text);
    }
}
