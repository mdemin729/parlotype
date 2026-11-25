using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using static Parlotype.Platform.Audio.CoreAudioInterop;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Enumerates macOS input devices through the CoreAudio hardware-abstraction
/// layer and notifies when the set or the default changes (ADR-078).
/// </summary>
/// <remarks>
/// A device's <see cref="MicrophoneInfo.Id"/> is its CoreAudio UID, not its
/// numeric object id: the UID survives a reboot and a replug, which is what a
/// saved microphone choice needs, and it is what
/// <see cref="CoreAudioCaptureService"/> hands to the audio queue.
/// <para>
/// <b>Nothing here may run on the caller's thread until CoreAudio is up.</b> The
/// first CoreAudio call in a process initialises the hardware layer, and for an
/// app the user has not yet answered the microphone question for, macOS shows
/// its permission prompt at that point and does not return until it is answered
/// (<c>docs/knowledge/coreaudio-first-call-can-block.md</c>).
/// This class is constructed on the UI thread at startup, so it makes that
/// first call on a background thread, reports no devices until it has
/// returned, and then raises <see cref="DevicesChanged"/> — the same signal a
/// plugged-in microphone sends, which every consumer already handles.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class CoreAudioMicrophoneEnumerator : IMicrophoneEnumerator
{
    private readonly ILogger<CoreAudioMicrophoneEnumerator> _logger;

    /// <summary>Kept in a field so the thunk CoreAudio calls is not collected.</summary>
    private readonly PropertyListener _listener;

    private readonly Lock _gate = new();
    private bool _listening;
    private volatile bool _ready;
    private volatile bool _disposed;

    public event EventHandler? DevicesChanged;

    public CoreAudioMicrophoneEnumerator(ILogger<CoreAudioMicrophoneEnumerator> logger)
    {
        _logger = logger;
        _listener = OnHardwareChanged;

        _ = Task.Run(Initialize);
    }

    /// <summary>
    /// Makes the process's first CoreAudio call, which can wait on the user for
    /// as long as the microphone prompt stays on screen.
    /// </summary>
    private void Initialize()
    {
        try
        {
            var devices = new PropertyAddress(PropertyDevices, ScopeGlobal);
            var defaultInput = new PropertyAddress(PropertyDefaultInputDevice, ScopeGlobal);

            var added = AudioObjectAddPropertyListener(SystemObject, ref devices, _listener, 0);
            var addedDefault = AudioObjectAddPropertyListener(SystemObject, ref defaultInput, _listener, 0);

            lock (_gate)
            {
                _listening = added == NoError && addedDefault == NoError;

                // Disposed while the call above was waiting: undo it and stay quiet.
                if (_disposed)
                {
                    RemoveListeners();
                    return;
                }

                _ready = true;
            }

            if (_listening)
                _logger.LogInformation("Microphone enumerator initialized");
            else
                _logger.LogWarning(
                    "Microphone enumerator initialized without change notifications (CoreAudio: {Devices}, {Default})",
                    DescribeStatus(added), DescribeStatus(addedDefault));

            // The list just went from "not known yet" to its real contents.
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not initialize the microphone enumerator");
        }
    }

    public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_ready)
            return [];

        var defaultId = GetDefaultInputDeviceId();
        var microphones = new List<MicrophoneInfo>();

        foreach (var deviceId in GetDeviceIds())
        {
            if (!HasInputStreams(deviceId))
                continue;

            if (Describe(deviceId, isDefault: deviceId == defaultId) is { } info)
                microphones.Add(info);
        }

        _logger.LogDebug("Enumerating microphones, found {Count} devices", microphones.Count);
        return microphones;
    }

    public MicrophoneInfo? GetDefaultMicrophone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_ready)
            return null;

        var defaultId = GetDefaultInputDeviceId();
        return defaultId == 0 ? null : Describe(defaultId, isDefault: true);
    }

    private static MicrophoneInfo? Describe(uint deviceId, bool isDefault)
    {
        var uid = ReadStringProperty(deviceId, PropertyDeviceUid);
        if (string.IsNullOrEmpty(uid))
            return null;

        var name = ReadStringProperty(deviceId, PropertyName);
        return new MicrophoneInfo(uid, string.IsNullOrWhiteSpace(name) ? uid : name, isDefault);
    }

    private static uint[] GetDeviceIds()
    {
        var address = new PropertyAddress(PropertyDevices, ScopeGlobal);
        if (AudioObjectGetPropertyDataSize(SystemObject, ref address, 0, 0, out var size) != NoError || size == 0)
            return [];

        var ids = new uint[size / sizeof(uint)];
        return AudioObjectGetPropertyData(SystemObject, ref address, 0, 0, ref size, ids) == NoError
            ? ids[..(int)(size / sizeof(uint))]
            : [];
    }

    /// <summary>
    /// A device records if it has at least one stream in the input scope. Output
    /// devices and the output half of an aggregate device have none.
    /// </summary>
    private static bool HasInputStreams(uint deviceId)
    {
        var address = new PropertyAddress(PropertyStreams, ScopeInput);
        return AudioObjectGetPropertyDataSize(deviceId, ref address, 0, 0, out var size) == NoError && size > 0;
    }

    private static uint GetDefaultInputDeviceId()
    {
        var address = new PropertyAddress(PropertyDefaultInputDevice, ScopeGlobal);
        var size = (uint)sizeof(uint);

        return AudioObjectGetPropertyData(SystemObject, ref address, 0, 0, ref size, out uint deviceId) == NoError
            ? deviceId
            : 0;
    }

    private int OnHardwareChanged(uint objectId, uint addressCount, nint addresses, nint clientData)
    {
        try
        {
            _logger.LogInformation("Audio input devices changed");
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // An exception must not unwind into CoreAudio's notification thread.
            _logger.LogError(ex, "A microphone change handler failed");
        }

        return NoError;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;

            // If initialization is still waiting, it removes its own listeners
            // when it returns; nothing here may call into CoreAudio before then.
            if (_ready)
                RemoveListeners();
        }
    }

    /// <summary>Must be called with <see cref="_gate"/> held.</summary>
    private void RemoveListeners()
    {
        if (!_listening)
            return;

        var devices = new PropertyAddress(PropertyDevices, ScopeGlobal);
        var defaultInput = new PropertyAddress(PropertyDefaultInputDevice, ScopeGlobal);
        AudioObjectRemovePropertyListener(SystemObject, ref devices, _listener, 0);
        AudioObjectRemovePropertyListener(SystemObject, ref defaultInput, _listener, 0);
        _listening = false;
    }
}
