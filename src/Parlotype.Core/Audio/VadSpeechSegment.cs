namespace Parlotype.Core.Audio;

/// <summary>Represents a detected speech segment as sample indices.</summary>
public sealed record VadSpeechSegment(int StartSample, int EndSample);
