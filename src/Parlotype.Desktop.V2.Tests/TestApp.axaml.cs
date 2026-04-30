using Avalonia;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.V2.Tests;

public class TestApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
