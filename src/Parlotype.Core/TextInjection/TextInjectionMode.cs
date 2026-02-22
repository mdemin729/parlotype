namespace Parlotype.Core.TextInjection;

/// <summary>Determines how transcribed text is injected into the target application.</summary>
public enum TextInjectionMode
{
    /// <summary>Clipboard-with-restore: paste via Ctrl+V then restore original clipboard.</summary>
    Clipboard,

    /// <summary>SharpHook: simulate text entry character-by-character via uiohook.</summary>
    SharpHook
}
