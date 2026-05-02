using Avalonia;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Tests;

public class TestApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
