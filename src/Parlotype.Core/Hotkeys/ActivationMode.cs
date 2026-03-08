namespace Parlotype.Core.Hotkeys;

/// <summary>Determines how the global hotkey activates recording.</summary>
public enum ActivationMode
{
    /// <summary>Recording starts on key-down, stops on key-up.</summary>
    PushToTalk,

    /// <summary>First press starts recording, second press stops it.</summary>
    Toggle
}
