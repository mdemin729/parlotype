namespace Parlotype.Core.TextInjection;

/// <summary>
/// Injects transcribed text into the currently focused application.
/// </summary>
public interface ITextInjectionService
{
    /// <summary>Injects the specified text into the target application.</summary>
    Task InjectTextAsync(string text, CancellationToken cancellationToken = default);
}
