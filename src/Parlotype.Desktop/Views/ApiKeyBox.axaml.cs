using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Single-frame API-key entry: a masked text field and a reveal-password
/// toggle presented as one cohesive control, mirroring the Fluent
/// <c>SplitButton</c> anatomy (input | 1px separator | button inside a shared
/// border) instead of a free-floating eye button. The field is write-only by
/// convention — consumers bind <see cref="Text"/> to a write-only entry
/// property and never populate it from storage.
/// </summary>
public partial class ApiKeyBox : UserControl
{
    /// <summary>The key text being entered. Two-way by default.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ApiKeyBox, string>(
            nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Watermark shown while the field is empty (e.g. "sk-…").</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<ApiKeyBox, string>(nameof(PlaceholderText), string.Empty);

    /// <summary>Whether the key characters are shown in clear text. Two-way by default.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<ApiKeyBox, bool>(
            nameof(IsRevealed), defaultBindingMode: BindingMode.TwoWay);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool IsRevealed
    {
        get => GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public ApiKeyBox()
    {
        InitializeComponent();
    }
}
