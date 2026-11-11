namespace Parlotype.Core.Feedback;

/// <summary>What the user is sending (ADR-070). Maps to the API's <c>kind</c> field.</summary>
public enum FeedbackKind
{
    /// <summary>An idea, a question or a comment — <c>"feedback"</c>.</summary>
    Feedback,

    /// <summary>Something that does not work — <c>"bug"</c>.</summary>
    Bug,
}
