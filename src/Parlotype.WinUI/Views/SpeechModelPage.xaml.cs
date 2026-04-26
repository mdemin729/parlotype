using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Parlotype.WinUI.ViewModels;

namespace Parlotype.WinUI.Views;

public sealed partial class SpeechModelPage : Page
{
    public SpeechModelViewModel ViewModel { get; }

    public SpeechModelPage()
    {
        ViewModel = App.Services.GetRequiredService<SpeechModelViewModel>();
        InitializeComponent();
    }
}

/// <summary>
/// Converts a <see cref="bool"/> cache status to a Segoe Fluent Icons glyph.
/// Cached → check mark (&#xE73E;), not cached → download icon (&#xE896;).
/// </summary>
internal sealed class CacheStatusToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? "\uE73E" : "\uE896";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
