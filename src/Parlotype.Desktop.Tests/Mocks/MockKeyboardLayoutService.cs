using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Keyboard-layout detection mock with a settable result. Null (the default)
/// models platforms where detection is unavailable.
/// </summary>
public sealed class MockKeyboardLayoutService : IKeyboardLayoutService
{
    public KeyboardLayoutInfo? Result { get; set; }

    public int DetectCalls { get; private set; }

    public KeyboardLayoutInfo? Detect()
    {
        DetectCalls++;
        return Result;
    }
}
