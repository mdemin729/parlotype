using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Views.Settings;

public partial class LanguagePickerView : UserControl
{
    public LanguagePickerView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
