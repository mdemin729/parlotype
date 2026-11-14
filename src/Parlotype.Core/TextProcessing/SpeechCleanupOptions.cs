namespace Parlotype.Core.TextProcessing;

/// <summary>How much of the speaker's voice survives into the written text.</summary>
public enum CleanupStyling
{
    /// <summary>All lowercase, apostrophes dropped, colloquialisms kept.</summary>
    Casual,

    /// <summary>The speaker's phrasing, with "I" and its contractions capitalized.</summary>
    SemiCasual,

    /// <summary>Standard written English; contractions kept, colloquialisms smoothed. The default.</summary>
    SemiFormal,

    /// <summary>Standard written English with contractions expanded.</summary>
    Formal,
}

/// <summary>Whether enumerable content may become a bulleted list.</summary>
public enum CleanupStructure
{
    /// <summary>Sentences and paragraphs only.</summary>
    Prose,

    /// <summary>Three or more enumerated items may be broken into bullets.</summary>
    Lists,
}

/// <summary>Where the text is going, which decides its layout.</summary>
public enum CleanupContext
{
    /// <summary>Flowing text.</summary>
    General,

    /// <summary>Greeting line, body and sign-off block separated by blank lines.</summary>
    Email,
}

/// <summary>
/// The user's choices for a speech cleanup pass (ADR-073). The three axes are
/// independent; every combination is valid.
/// </summary>
public sealed record SpeechCleanupOptions(
    CleanupStyling Styling = CleanupStyling.SemiFormal,
    CleanupStructure Structure = CleanupStructure.Prose,
    CleanupContext Context = CleanupContext.General)
{
    public static SpeechCleanupOptions Default { get; } = new();
}
