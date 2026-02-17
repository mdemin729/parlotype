namespace Parlotype.Core.Speech;

public enum WaitTimeOption
{
    Instant,
    VeryShort,
    Short,
    Medium,
    Long,
    Extended,
    VeryLong
}

public static class WaitTimeOptionExtensions
{
    public static string GetDisplayName(this WaitTimeOption option) => option switch
    {
        WaitTimeOption.Instant => "Instant",
        WaitTimeOption.VeryShort => "Very Short",
        WaitTimeOption.Short => "Short",
        WaitTimeOption.Medium => "Medium",
        WaitTimeOption.Long => "Long",
        WaitTimeOption.Extended => "Extended",
        WaitTimeOption.VeryLong => "Very Long",
        _ => option.ToString()
    };

    public static double GetSeconds(this WaitTimeOption option) => option switch
    {
        WaitTimeOption.Instant => 0.1,
        WaitTimeOption.VeryShort => 0.2,
        WaitTimeOption.Short => 0.3,
        WaitTimeOption.Medium => 0.5,
        WaitTimeOption.Long => 1.0,
        WaitTimeOption.Extended => 2.0,
        WaitTimeOption.VeryLong => 3.0,
        _ => 0.5
    };
}
