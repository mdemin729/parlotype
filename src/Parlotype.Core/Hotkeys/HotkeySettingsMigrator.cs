using Parlotype.Core.Settings;

namespace Parlotype.Core.Hotkeys;

/// <summary>
/// One-shot migration from the single-chord hotkey settings
/// (<see cref="SettingsKeys.HotkeyModifiers"/> + <see cref="SettingsKeys.HotkeyKey"/>
/// + <see cref="SettingsKeys.ActivationMode"/>) to the multi-gesture
/// <see cref="SettingsKeys.HotkeyBindings"/> list.
///
/// <para>Idempotent — guarded by the presence of the new key.</para>
/// </summary>
public static class HotkeySettingsMigrator
{
    /// <summary>The chord that used to ship as the only default.</summary>
    internal static HotkeyBinding LegacyDefaultChord { get; } =
        new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");

    /// <summary>
    /// Returns the binding set to use, writing it to settings when a migration
    /// actually happened.
    /// </summary>
    public static async Task<IReadOnlyList<DictationHotkey>> LoadOrMigrateAsync(
        ISettingsService settings, CancellationToken ct = default)
    {
        var stored = await settings.GetAsync<List<string>>(SettingsKeys.HotkeyBindings, ct).ConfigureAwait(false);
        if (stored is not null)
        {
            var decoded = HotkeyBindingCodec.DecodeAll(stored);

            // An empty list is a decision, not an absent setting: the settings
            // page lets users remove every binding and drive dictation from the
            // widget instead, and handing them three global hotkeys back on the
            // next launch would undo that silently. A list whose entries all
            // fail to decode is a damaged file rather than a choice, so that
            // case falls through to the defaults below.
            if (decoded.Count > 0 || stored.Count == 0)
                return decoded;
        }

        var migrated = await BuildFromLegacyAsync(settings, ct).ConfigureAwait(false);
        await settings.SetAsync(SettingsKeys.HotkeyBindings, HotkeyBindingCodec.EncodeAll(migrated), ct)
            .ConfigureAwait(false);

        return migrated;
    }

    private static async Task<IReadOnlyList<DictationHotkey>> BuildFromLegacyAsync(
        ISettingsService settings, CancellationToken ct)
    {
        var modifiersStr = await settings.GetAsync<string>(SettingsKeys.HotkeyModifiers, ct).ConfigureAwait(false);
        var key = await settings.GetAsync<string>(SettingsKeys.HotkeyKey, ct).ConfigureAwait(false);

        if (!Enum.TryParse<HotkeyModifiers>(modifiersStr, out var modifiers) || string.IsNullOrWhiteSpace(key))
            return DictationHotkeyDefaults.All;

        var legacyChord = new HotkeyBinding(modifiers, key);
        if (!legacyChord.IsValid || legacyChord == LegacyDefaultChord)
            return DictationHotkeyDefaults.All;

        // The user deliberately chose this chord, so it stays their only binding.
        // Silently adding the new gesture defaults would hand them global
        // hotkeys they never asked for, on keys they may already use elsewhere.
        var modeStr = await settings.GetAsync<string>(SettingsKeys.ActivationMode, ct).ConfigureAwait(false);
        var mode = Enum.TryParse<ActivationMode>(modeStr, out var parsed)
            ? parsed
            : ActivationMode.PushToTalk;

        return [DictationHotkey.Chord(legacyChord, mode)];
    }
}
