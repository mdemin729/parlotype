using System.Windows.Input;
using Parlotype.Core.Audio;

namespace Parlotype.Desktop.ViewModels;

public sealed class MicrophoneDisplayItem(MicrophoneInfo info, ICommand selectCommand)
{
    public MicrophoneInfo Info { get; } = info;
    public string Name { get; } = info.Name;
    public bool IsDefault { get; } = info.IsDefault;
    public ICommand SelectCommand { get; } = selectCommand;
}
