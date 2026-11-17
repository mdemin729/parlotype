using Parlotype.Core.Settings;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>Scripted permission states, recording what was requested and which settings pane was opened.</summary>
public sealed class MockSystemPermissionService : ISystemPermissionService
{
    public Dictionary<SystemPermission, SystemPermissionState> States { get; } = [];

    public List<SystemPermission> Requested { get; } = [];
    public List<SystemPermission> SettingsOpened { get; } = [];

    public SystemPermissionState GetState(SystemPermission permission) =>
        States.GetValueOrDefault(permission, SystemPermissionState.Granted);

    public Task<SystemPermissionState> RequestAsync(
        SystemPermission permission, CancellationToken cancellationToken = default)
    {
        Requested.Add(permission);
        return Task.FromResult(GetState(permission));
    }

    public bool OpenSettings(SystemPermission permission)
    {
        SettingsOpened.Add(permission);
        return true;
    }
}
