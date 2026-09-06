using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// One row in the interface-language picker (ADR-064). Carries its own command,
/// following the flyout-safe display-item pattern used by
/// <see cref="MicrophoneDisplayItem"/> and friends.
/// </summary>
/// <param name="settingValue">
/// What gets written to settings — a culture name, or
/// <c>SupportedUiLanguages.SystemSettingValue</c> for the "System default" row.
/// </param>
/// <param name="label">
/// The row's title. Endonym for a concrete language ("Русский"), so it is legible
/// from whatever language the user is currently stuck in; localized copy only for
/// the "System default" row, which names a behaviour rather than a language.
/// </param>
public sealed partial class UiLanguageDisplayItem(
    string settingValue,
    string label,
    ICommand selectCommand)
    : ObservableObject
{
    public string SettingValue { get; } = settingValue;

    [ObservableProperty]
    private string _label = label;

    /// <summary>
    /// Second line, or null for none. Used by the "System default" row to say
    /// which language it currently resolves to — without it, the row is a promise
    /// the user cannot check.
    /// </summary>
    [ObservableProperty]
    private string? _detail;

    [ObservableProperty]
    private bool _isSelected;

    public ICommand SelectCommand { get; } = selectCommand;

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    partial void OnDetailChanged(string? value) => OnPropertyChanged(nameof(HasDetail));
}
