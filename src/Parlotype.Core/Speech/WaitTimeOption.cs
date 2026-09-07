namespace Parlotype.Core.Speech;

public enum WaitTimeOption
{
    Medium,
    Long,
    Extended,
    VeryLong
}

public static class WaitTimeOptionExtensions
{
    // GetDisplayName moved to Parlotype.Desktop (WaitTimeDisplayItem) in ADR-064.
    // "Medium", "Long", "Very Long" are prose shown to the user and have to be
    // translated; Core has no resources and no business holding UI copy. What
    // stays here is the part the pipeline actually runs on.

    public static double GetSeconds(this WaitTimeOption option) => option switch
    {
        WaitTimeOption.Medium => 0.5,
        WaitTimeOption.Long => 1.0,
        WaitTimeOption.Extended => 2.0,
        WaitTimeOption.VeryLong => 3.0,
        _ => 0.5
    };
}
