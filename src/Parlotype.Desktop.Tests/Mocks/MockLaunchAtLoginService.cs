using Parlotype.Core.Settings;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// In-memory <see cref="ILaunchAtLoginService"/> for UI tests. Records what was
/// asked of it and lets a test pin the reported state — including the two cases
/// the real registry produces but a test cannot: an unsupported build and a
/// Windows-side veto.
/// </summary>
public sealed class MockLaunchAtLoginService : ILaunchAtLoginService
{
    private bool _registered;

    public MockLaunchAtLoginService(bool isSupported = true, bool registered = false)
    {
        IsSupported = isSupported;
        _registered = registered;
    }

    public bool IsSupported { get; }

    /// <summary>When set, every read reports this instead of the recorded state.</summary>
    public LaunchAtLoginState? ForcedState { get; set; }

    /// <summary>Number of <see cref="SetEnabled"/> calls, to assert idempotence.</summary>
    public int SetCount { get; private set; }

    public LaunchAtLoginState GetState()
    {
        if (!IsSupported)
            return LaunchAtLoginState.Unsupported;

        return ForcedState
            ?? (_registered ? LaunchAtLoginState.Enabled : LaunchAtLoginState.Disabled);
    }

    public bool SetEnabled(bool enabled)
    {
        SetCount++;
        if (!IsSupported)
            return false;

        _registered = enabled;
        return true;
    }
}
