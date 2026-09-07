using Avalonia.Data;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.Markup;

/// <summary>
/// Resolves a resx key in markup: <c>Text="{loc:Tr Settings_Theme_Title}"</c>
/// (ADR-064). The one way AXAML gets user-facing text.
/// </summary>
/// <remarks>
/// <para>
/// Returns a <see cref="CompiledBinding"/> over the key's <see cref="LocalizedString"/>,
/// so switching the interface language updates text in place — no restart, and no
/// reflection binding. The expression is a plain property access on a known type,
/// which is what lets <see cref="CompiledBinding.Create{TIn,TOut}"/> compile it;
/// binding to an indexer or a method would have forced
/// <see cref="ReflectionBinding"/> and broken the project's compiled-bindings rule.
/// </para>
/// <para>
/// The binding's source is the key's entry, not the DataContext, so this works
/// anywhere markup does — inside DataTemplates, styles and flyouts that have no
/// view model of their own.
/// </para>
/// </remarks>
/// <param name="key">
/// The resx key. Not verified at compile time; the localization parity test
/// catches keys that no longer exist, and a miss renders the key itself rather
/// than failing (ADR-056's rule).
/// </param>
public sealed class TrExtension(string key)
{
    /// <summary>The resx key to resolve.</summary>
    public string Key { get; set; } = key;

    public object ProvideValue(IServiceProvider serviceProvider) =>
        CompiledBinding.Create<LocalizedString, string>(
            s => s.Value,
            Localizer.Instance.Entry(Key));
}
