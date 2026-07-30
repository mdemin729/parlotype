namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Builds the one-line reminder shown on the recording widget. Cheap to render
/// and it answers the most common support question there is — "what was my
/// hotkey again?".
/// </summary>
public static class HotkeyHint
{
    private const string CancelSuffix = "Esc to cancel";

    /// <summary>
    /// Describes how to start dictating, preferring the push-to-talk binding
    /// since that is the one users reach for mid-sentence.
    /// </summary>
    public static string Describe(IReadOnlyList<DictationHotkey>? bindings)
    {
        var valid = bindings?.Where(b => b.IsValid).ToList();
        if (valid is not { Count: > 0 })
            return "No dictation hotkey set";

        var primary = valid.FirstOrDefault(b => b.Mode == ActivationMode.PushToTalk) ?? valid[0];

        var verb = primary.Mode == ActivationMode.PushToTalk ? "to talk" : "to dictate";
        return $"{primary.DisplayString} {verb} · {CancelSuffix}";
    }
}
