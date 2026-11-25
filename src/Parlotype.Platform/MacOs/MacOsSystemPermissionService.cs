using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.MacOs;

/// <summary><see cref="ISystemPermissionService"/> over macOS's privacy database (ADR-078).</summary>
[SupportedOSPlatform("macos")]
public sealed class MacOsSystemPermissionService : ISystemPermissionService
{
    private readonly ILogger<MacOsSystemPermissionService> _logger;

    public MacOsSystemPermissionService(ILogger<MacOsSystemPermissionService> logger)
    {
        _logger = logger;
    }

    public SystemPermissionState GetState(SystemPermission permission)
    {
        try
        {
            return permission switch
            {
                SystemPermission.Accessibility => MacOsPrivacy.IsAccessibilityGranted
                    ? SystemPermissionState.Granted
                    : SystemPermissionState.Denied,
                SystemPermission.Microphone => MacOsPrivacy.MicrophoneState,
                _ => SystemPermissionState.Granted,
            };
        }
        catch (Exception ex)
        {
            // An unreadable grant must not take the app down. Reporting it as
            // granted lets the real operation run and fail with its own error.
            _logger.LogWarning(ex, "Could not read the {Permission} permission", permission);
            return SystemPermissionState.Granted;
        }
    }

    public async Task<SystemPermissionState> RequestAsync(
        SystemPermission permission, CancellationToken cancellationToken = default)
    {
        try
        {
            if (permission == SystemPermission.Accessibility)
            {
                if (!MacOsPrivacy.IsAccessibilityGranted)
                    MacOsPrivacy.PromptForAccessibility();
            }
            else if (permission == SystemPermission.Microphone)
            {
                await MacOsPrivacy.RequestMicrophoneAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not request the {Permission} permission", permission);
        }

        return GetState(permission);
    }

    public bool OpenSettings(SystemPermission permission)
    {
        var opened = MacOsPrivacy.OpenSettings(permission);
        if (!opened)
            _logger.LogWarning("Could not open System Settings for the {Permission} permission", permission);

        return opened;
    }
}

/// <summary>
/// <see cref="ISystemPermissionService"/> for platforms that do not gate the
/// hotkey hook or the microphone behind a per-app grant.
/// </summary>
public sealed class NoOpSystemPermissionService : ISystemPermissionService
{
    public SystemPermissionState GetState(SystemPermission permission) => SystemPermissionState.Granted;

    public Task<SystemPermissionState> RequestAsync(
        SystemPermission permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemPermissionState.Granted);

    public bool OpenSettings(SystemPermission permission) => false;
}
