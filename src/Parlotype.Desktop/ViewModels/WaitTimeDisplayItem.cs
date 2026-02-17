using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public sealed class WaitTimeDisplayItem(WaitTimeOption option)
{
    public WaitTimeOption Option { get; } = option;
    public string DisplayName { get; } = option.GetDisplayName();
    public string SecondsText { get; } = $"{option.GetSeconds():F1} seconds";
}
