using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.Resources;

/// <summary>
/// The live view of one resource key (ADR-064). Holds no copy of its own — every
/// read goes back to the <see cref="ResourceManager"/> at the current culture —
/// and raises <see cref="INotifyPropertyChanged"/> when the interface language
/// changes, which is what makes bound text update in place instead of at the next
/// app start.
/// </summary>
/// <remarks>
/// One instance per key, created on first use and kept for the process lifetime by
/// <see cref="Localizer"/>. This exists so that <c>{loc:Tr}</c> can bind to a plain
/// property: an indexer or a method call would force a reflection binding, which
/// the project bans (CLAUDE.md). <see cref="Value"/> is an ordinary CLR property,
/// so the binding is compiled like every other binding in the app.
/// </remarks>
public sealed partial class LocalizedString : ObservableObject
{
    internal LocalizedString(string key) => Key = key;

    /// <summary>The resx key this tracks.</summary>
    public string Key { get; }

    /// <summary>The key's text in the current interface language.</summary>
    public string Value => Localizer.Lookup(Key);

    /// <summary>Re-reads <see cref="Value"/> — called when the culture changes.</summary>
    internal void Invalidate() => OnPropertyChanged(nameof(Value));

    public override string ToString() => Value;
}

/// <summary>
/// Owns the interface language at runtime (ADR-064): resolves keys against
/// <c>Strings.resx</c> and its satellites, and switches the whole UI to another
/// language in place.
/// </summary>
/// <remarks>
/// <para>
/// A singleton with process-wide static state, deliberately. The alternative — a
/// DI service threaded to every view — cannot serve XAML: a markup extension is
/// constructed by the XAML loader with no access to the container, and the copy it
/// resolves is needed in DataTemplates, styles and flyouts that are outside any
/// view model's reach. <see cref="UiLanguageService"/> is the DI-visible seam that
/// drives this; nothing else should call <see cref="SetCulture"/>.
/// </para>
/// <para>
/// Culture resolution is left to .NET. Setting <see cref="CultureInfo.CurrentUICulture"/>
/// to <c>ru-RU</c> makes <see cref="ResourceManager"/> walk <c>ru-RU</c> → <c>ru</c>
/// → neutral on its own, so an untranslated key, a missing satellite assembly and
/// an unsupported OS language all land on English without any code here.
/// </para>
/// </remarks>
public sealed class Localizer
{
    private static readonly ResourceManager Manager =
        new("Parlotype.Desktop.Resources.Strings", typeof(Localizer).Assembly);

    private readonly ConcurrentDictionary<string, LocalizedString> _entries = new(StringComparer.Ordinal);

    public static Localizer Instance { get; } = new();

    private Localizer()
    {
    }

    /// <summary>
    /// Raised after the interface language changes, once every bound string has
    /// been invalidated. Surfaces that live-switch moment to things a binding
    /// cannot reach — the tray menu, whose <c>NativeMenu</c> headers are built
    /// once, and the Settings navigation list, whose rows are snapshots.
    /// </summary>
    public event EventHandler? CultureChanged;

    /// <summary>The culture the interface is currently drawn in.</summary>
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    /// <summary>
    /// The live string for <paramref name="key"/>. Repeated calls with the same key
    /// return the same instance, so a key used by twenty controls costs one object
    /// and one notification.
    /// </summary>
    public LocalizedString Entry(string key) =>
        _entries.GetOrAdd(key, static k => new LocalizedString(k));

    /// <summary>
    /// The text for <paramref name="key"/> right now, without tracking it. For
    /// one-shot reads — a log line, a dialog built and thrown away. UI that stays
    /// on screen should go through <see cref="Entry"/> or the generated
    /// <see cref="Strings"/> accessor instead.
    /// </summary>
    /// <remarks>
    /// A missing key answers with the key itself rather than throwing (the
    /// behaviour ADR-056 established): a stale resx should show an obviously wrong
    /// label, never take the window down.
    /// </remarks>
    public static string Lookup(string key)
    {
        try
        {
            return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    /// <summary>
    /// Switches the interface to <paramref name="culture"/> and refreshes every
    /// bound string. Safe to call from any thread: the notifications land on
    /// bindings and on view models that rebuild <c>ObservableCollection</c>s, so
    /// they are marshalled to the UI thread.
    /// </summary>
    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (Equals(culture, CurrentCulture))
            return;

        CurrentCulture = culture;

        // DefaultThreadCurrentUICulture covers threads that start later; the
        // assignment to CurrentUICulture covers this one, which is the UI thread
        // and the only one that reads resources.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Note what is *not* touched: CurrentCulture. Dates, numbers and sort
        // order keep following the user's Windows regional settings. Someone
        // running an English Windows in a Russian interface still wants their own
        // date format, and the interface language is not a claim about where they
        // are.

        // Everything below reaches UI-affine state — LocalizedString notifies
        // bindings, and CultureChanged subscribers rebuild ObservableCollections
        // (CLAUDE.md: background-thread mutations must dispatch to the UI
        // thread). SetCulture is called from the UI thread in the app, where
        // Invoke runs inline; the dispatch exists for every other caller.
        if (Dispatcher.UIThread.CheckAccess())
            Notify();
        else
            Dispatcher.UIThread.Invoke(Notify);

        void Notify()
        {
            foreach (var entry in _entries.Values)
                entry.Invalidate();

            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
