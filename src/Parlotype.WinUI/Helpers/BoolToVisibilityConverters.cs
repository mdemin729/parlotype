using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Parlotype.WinUI.Helpers;

/// <summary>
/// Converts a <see cref="bool"/> value to <see cref="Visibility"/>.
/// <c>true</c> → <see cref="Visibility.Visible"/>, <c>false</c> → <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

/// <summary>
/// Converts a <see cref="bool"/> value to the inverse <see cref="Visibility"/>.
/// <c>true</c> → <see cref="Visibility.Collapsed"/>, <c>false</c> → <see cref="Visibility.Visible"/>.
/// </summary>
public sealed class BoolToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Collapsed;
}
