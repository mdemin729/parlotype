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
    public static string GetDisplayName(this WaitTimeOption option) => option switch
    {
        WaitTimeOption.Medium => "Medium",
        WaitTimeOption.Long => "Long",
        WaitTimeOption.Extended => "Extended",
        WaitTimeOption.VeryLong => "Very Long",
        _ => option.ToString()
    };

    public static double GetSeconds(this WaitTimeOption option) => option switch
    {
        WaitTimeOption.Medium => 0.5,
        WaitTimeOption.Long => 1.0,
        WaitTimeOption.Extended => 2.0,
        WaitTimeOption.VeryLong => 3.0,
        _ => 0.5
    };
}
