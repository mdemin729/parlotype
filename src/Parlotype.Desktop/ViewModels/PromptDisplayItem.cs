using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class PromptDisplayItem(
    PromptTemplate prompt,
    ICommand selectCommand,
    ICommand editCommand,
    ICommand duplicateCommand,
    ICommand deleteCommand)
    : ObservableObject
{
    public string Id { get; } = prompt.Id;
    public bool IsBuiltIn { get; } = prompt.IsBuiltIn;
    public bool CanEdit => !IsBuiltIn;

    public ICommand SelectCommand { get; } = selectCommand;
    public ICommand EditCommand { get; } = editCommand;
    public ICommand DuplicateCommand { get; } = duplicateCommand;
    public ICommand DeleteCommand { get; } = deleteCommand;

    /// <summary>
    /// The row's label. For the one prompt that ships with the app this is
    /// translated copy; for every prompt the user made it is their own text,
    /// verbatim.
    /// </summary>
    /// <remarks>
    /// The built-in's name lives in <c>JsonPromptTemplateRegistry</c> as an
    /// English literal, which is right — Platform has no resources — but it
    /// reached the screen untranslated in all 24 languages, because it is
    /// neither in AXAML nor in resx and so no check could see it. Same split as
    /// the rest of ADR-064: Platform supplies the identity (<c>IsBuiltIn</c>),
    /// Desktop chooses the words.
    /// </remarks>
    [ObservableProperty]
    private string _name = Describe(prompt);

    private static string Describe(PromptTemplate prompt) =>
        prompt.IsBuiltIn ? Strings.Settings_Prompts_BuiltInName : prompt.Name;

    /// <summary>
    /// Re-reads the built-in's translated name after a language change. A
    /// user-made prompt's name is their data and is left exactly as it is.
    /// </summary>
    public void RefreshDisplayName()
    {
        if (IsBuiltIn)
            Name = Strings.Settings_Prompts_BuiltInName;
    }

    [ObservableProperty]
    private string _text = prompt.Text;

    [ObservableProperty]
    private bool _isSelected;
}
