namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Picks and (in its invariant form) describes the one-line reminder shown on
/// the recording widget. Answers the most common support question there is —
/// "what was my hotkey again?".
/// </summary>
/// <remarks>
/// <see cref="Describe"/> is the invariant English sentence, kept for the two
/// existing tests that pin its exact wording; nothing reads it for the UI any
/// more. The window shows <c>HotkeyText.Hint</c> (Desktop) instead, built from
/// <see cref="SelectPrimary"/>'s data — Core has no resources, so the words
/// live where the resources do (ADR-064 amendment).
/// </remarks>
public static class HotkeyHint
{
    private const string CancelSuffix = "Esc to cancel";

    /// <summary>
    /// The binding the hint should describe, preferring the push-to-talk one
    /// since that is the one users reach for mid-sentence. Null when nothing
    /// valid is configured.
    /// </summary>
    public static DictationHotkey? SelectPrimary(IReadOnlyList<DictationHotkey>? bindings)
    {
        var valid = bindings?.Where(b => b.IsValid).ToList();
        if (valid is not { Count: > 0 })
            return null;

        return valid.FirstOrDefault(b => b.Mode == ActivationMode.PushToTalk) ?? valid[0];
    }

    /// <summary>
    /// Describes how to start dictating, preferring the push-to-talk binding
    /// since that is the one users reach for mid-sentence.
    /// </summary>
    public static string Describe(IReadOnlyList<DictationHotkey>? bindings)
    {
        if (SelectPrimary(bindings) is not { } primary)
            return "No dictation hotkey set";

        var verb = primary.Mode == ActivationMode.PushToTalk ? "to talk" : "to dictate";
        return $"{primary.DisplayString} {verb} · {CancelSuffix}";
    }
}
