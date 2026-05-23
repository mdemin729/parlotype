using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

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

    [ObservableProperty]
    private string _name = prompt.Name;

    [ObservableProperty]
    private string _text = prompt.Text;

    [ObservableProperty]
    private bool _isSelected;
}
