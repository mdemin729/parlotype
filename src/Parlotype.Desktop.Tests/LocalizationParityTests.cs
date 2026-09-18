using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Parlotype.Core.Localization;
using Parlotype.Desktop.Resources;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Every shipped language stays complete and in step with the neutral resx
/// (ADR-064). `scripts/check-localization.ps1` runs the same checks for the
/// Claude Code hooks; these are here so CI and a plain `dotnet test` enforce them
/// too — a hook only covers sessions that go through Claude.
/// </summary>
public class LocalizationParityTests
{
    private static readonly string ResourcesDirectory =
        Path.Combine(RepoRoot(), "src", "Parlotype.Desktop", "Resources");

    public static TheoryData<string> TranslatedCultures()
    {
        var data = new TheoryData<string>();
        foreach (var language in SupportedUiLanguages.Translated)
            data.Add(language.CultureName);
        return data;
    }

    [Fact]
    public void EveryRegisteredLanguage_HasAResourceFile()
    {
        foreach (var language in SupportedUiLanguages.Translated)
        {
            Assert.True(
                File.Exists(SatellitePath(language.CultureName)),
                $"{language.CultureName} is registered in SupportedUiLanguages but "
                + $"Strings.{language.CultureName}.resx does not exist.");
        }
    }

    [Fact]
    public void TheNeutralLanguage_HasNoSatelliteFile()
    {
        // Strings.resx *is* English. A Strings.en.resx would shadow it for en-US
        // users and silently diverge from the file everything else falls back to.
        Assert.False(
            File.Exists(SatellitePath(SupportedUiLanguages.NeutralCultureName)),
            "Strings.en.resx must not exist — the neutral Strings.resx is English.");
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void EveryLanguage_TranslatesEveryKey(string culture)
    {
        var neutral = ReadResx(NeutralPath());
        var translated = ReadResx(SatellitePath(culture));

        var missing = neutral.Keys.Where(k => !translated.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(
            missing.Count == 0,
            $"Strings.{culture}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void EveryLanguage_HasNoOrphanKeys(string culture)
    {
        var neutral = ReadResx(NeutralPath());
        var translated = ReadResx(SatellitePath(culture));

        var orphans = translated.Keys.Where(k => !neutral.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(
            orphans.Count == 0,
            $"Strings.{culture}.resx has {orphans.Count} key(s) the neutral file no longer has: "
            + string.Join(", ", orphans));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void EveryLanguage_KeepsThePlaceholdersItWasGiven(string culture)
    {
        // A translation that drops {1} throws FormatException at runtime, in
        // production, in the one language nobody on the team reads.
        var neutral = ReadResx(NeutralPath());
        var translated = ReadResx(SatellitePath(culture));

        foreach (var (key, value) in neutral)
        {
            if (!translated.TryGetValue(key, out var translation))
                continue; // reported by EveryLanguage_TranslatesEveryKey

            Assert.Equal(Placeholders(value), Placeholders(translation));
        }
    }

    [Fact]
    public void NoKeyIsAccidentallyUntranslated()
    {
        // Not a hard rule — "Esc", "Parlotype" and the like are legitimately
        // identical. But a long sentence copied verbatim into a locale is almost
        // always an unfinished translation, so it has to be deliberate.
        //
        // Keyed by culture, not a single shared list: an exemption is a claim
        // about *one* language. A global list would excuse the key in all 25 at
        // once, including the languages that simply forgot it — which is the
        // exact failure this test exists to catch. Empty today, on purpose. Add
        // an entry only with a stated reason.
        Dictionary<string, string[]> deliberatelyIdentical = new(StringComparer.Ordinal)
        {
            // ["mt"] = ["Some_Key"],  // e.g. Maltese borrows the English term
        };

        var neutral = ReadResx(NeutralPath());

        foreach (var language in SupportedUiLanguages.Translated)
        {
            var translated = ReadResx(SatellitePath(language.CultureName));
            var exempt = deliberatelyIdentical.TryGetValue(language.CultureName, out var keys)
                ? keys
                : [];

            var suspicious = neutral
                .Where(pair => pair.Value.Length > 25)
                .Where(pair => !exempt.Contains(pair.Key))
                .Where(pair => translated.TryGetValue(pair.Key, out var t) && t == pair.Value)
                .Select(pair => pair.Key)
                .OrderBy(k => k)
                .ToList();

            Assert.True(
                suspicious.Count == 0,
                $"Strings.{language.CultureName}.resx repeats the English text verbatim for "
                + $"{suspicious.Count} key(s): {string.Join(", ", suspicious)}. Translate them, or "
                + $"add the key to deliberatelyIdentical[\"{language.CultureName}\"] with a reason.");
        }
    }

    [Fact]
    public void EveryRegisteredLanguage_IsAWellFormedNeutralCulture()
    {
        // The registry is hand-edited once per language and read by everything
        // else, so a slip here is easy to make and invisible until a user reports
        // an English window. The resx parity tests cannot see it: they only
        // compare files to each other, and a bogus culture name fails at runtime,
        // not at build.
        var seenCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEndonyms = new Dictionary<string, string>(StringComparer.Ordinal);

        // CultureInfo.GetCultureInfo is NOT a validity check. Given any
        // well-formed code it manufactures a culture rather than throwing, so
        // "zz" and "ls" (a transposition of "sl") both come back as perfectly
        // happy neutral CultureInfo objects. Measured, not assumed — an earlier
        // version of this test asserted only that the lookup did not throw, and
        // passed with a row reading new("zz", "Nonsense"). Membership in the
        // predefined set is what actually answers the question.
        var predefinedNeutrals = CultureInfo
            .GetCultures(CultureTypes.NeutralCultures)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var language in SupportedUiLanguages.All)
        {
            var culture = CultureInfo.GetCultureInfo(language.CultureName);

            Assert.True(
                culture.IsNeutralCulture,
                $"'{language.CultureName}' is a specific culture. Ship the neutral code "
                + "instead — .NET's satellite fallback serves every region from a neutral "
                + "satellite, but never the other way round.");

            Assert.True(
                predefinedNeutrals.Contains(language.CultureName),
                $"'{language.CultureName}' is not a language .NET knows. Check it against "
                + "plans/2026-09-17-twenty-five-ui-languages/research.md — a transposed "
                + "code such as 'ls' for 'sl' is accepted by GetCultureInfo and would "
                + "otherwise ship a locale nobody's system will ever ask for.");

            Assert.False(
                string.IsNullOrWhiteSpace(language.EndonymName),
                $"'{language.CultureName}' has no endonym.");

            // Deliberately NOT asserted against CultureInfo.NativeName: .NET
            // returns "français", "čeština", "polski" lowercase, because those
            // languages lowercase their own name in running prose. As a list
            // label this app capitalizes — it ships "Español", not "español" — so
            // most endonyms differ from NativeName by their first letter on
            // purpose, and pinning them to NativeName would force the wrong copy
            // into the picker.

            Assert.True(
                seenCultures.Add(language.CultureName),
                $"'{language.CultureName}' is registered twice.");

            Assert.False(
                seenEndonyms.TryGetValue(language.EndonymName, out var owner),
                $"'{language.EndonymName}' labels both '{owner}' and '{language.CultureName}'. "
                + "Slovak and Slovenian are the pair most likely to collide.");
            seenEndonyms[language.EndonymName] = language.CultureName;

            Assert.Equal(
                language.CultureName,
                SupportedUiLanguages.MatchSystemCulture(language.CultureName)?.CultureName);
        }
    }

    [Fact]
    public void EverySettingsSection_ThatComposesCopyInCSharp_RefreshesItOnCultureChange()
    {
        // The live-switch bug that keeps coming back (ADR-064): text bound through
        // {loc:Tr} re-reads itself, but anything a view model builds in C# — a
        // status line, a computed label, an ObservableCollection of strings — is a
        // snapshot. Nothing fails, nothing throws; the page simply keeps the
        // language the app started in, forever, and only a human switching
        // languages and looking at that exact page ever notices.
        //
        // Six of nineteen sections had it at the time this test was written, and
        // two of those were found by a user, not by us. Hence a structural check:
        // if a section reaches for Strings beyond its own Title, it has to say how
        // it refreshes.
        var exempt = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PromptSettingsViewModel"] =
                "Its only Strings use names a *copied prompt* (\"{0} (copy)\"). That name is "
                + "written to settings and becomes the user's own data — re-translating it on "
                + "a language switch would rename something they own.",
        };

        var directory = Path.Combine(
            RepoRoot(), "src", "Parlotype.Desktop", "ViewModels", "Settings");
        var problems = new List<string>();

        foreach (var path in Directory.EnumerateFiles(directory, "*ViewModel.cs").Order())
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var source = File.ReadAllText(path);

            if (!source.Contains("SettingsSectionViewModelBase", StringComparison.Ordinal))
                continue;

            // "Title => Strings.X" is the one use the base class already handles.
            var uses = Regex.Matches(source, @"Strings\.").Count
                - Regex.Matches(source, @"Title\s*=>\s*Strings\.").Count;

            if (uses <= 0)
                continue;

            if (source.Contains("override void OnCultureChanged", StringComparison.Ordinal))
                continue;

            if (exempt.ContainsKey(name))
                continue;

            problems.Add(
                $"{name} composes UI copy in C# ({uses} Strings reference(s) beyond Title) but "
                + "does not override OnCultureChanged, so that copy will keep the language the "
                + "app started in. Override it — calling base — or add the class to 'exempt' "
                + "above with the reason its text must NOT follow the interface language.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, problems));
    }

    [Fact]
    public void GeneratedAccessor_CoversEveryKey_InBothDirections()
    {
        // Catches "added the key but forgot to run gen-strings.ps1", and its
        // mirror, without shelling out to PowerShell from a test.
        var keys = ReadResx(NeutralPath()).Keys.ToHashSet(StringComparer.Ordinal);

        var properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = keys.Except(properties).Order().ToList();
        Assert.True(
            missing.Count == 0,
            $"Strings.cs is out of date — run: pwsh scripts/gen-strings.ps1. Missing: {string.Join(", ", missing)}");

        var stale = properties.Except(keys).Order().ToList();
        Assert.True(
            stale.Count == 0,
            $"Strings.cs has accessors for keys no longer in the resx — run: pwsh scripts/gen-strings.ps1. Stale: {string.Join(", ", stale)}");
    }

    [Fact]
    public void EveryCompositeKey_HasATypedFormatHelper()
    {
        // The helper is what makes a wrong argument count a compile error rather
        // than a FormatException in front of a user.
        var methods = typeof(Strings)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(m => m.Name, m => m.GetParameters().Length, StringComparer.Ordinal);

        foreach (var (key, value) in ReadResx(NeutralPath()))
        {
            var placeholders = Regex.Matches(value, @"\{(\d+)")
                .Select(m => int.Parse(m.Groups[1].Value))
                .Distinct()
                .Count();

            if (placeholders == 0)
                continue;

            Assert.True(
                methods.TryGetValue($"Format_{key}", out var arity),
                $"'{key}' contains placeholders but has no Format_{key} helper — run: pwsh scripts/gen-strings.ps1");
            Assert.Equal(placeholders, arity);
        }
    }

    [Fact]
    public void EveryTrKeyUsedInMarkup_ExistsInTheResx()
    {
        // A typo in {loc:Tr Setings_Theme_Title} does not fail the build and does
        // not throw — Localizer answers with the key name, so the window renders
        // the raw key. Only a check like this catches it.
        var keys = ReadResx(NeutralPath()).Keys.ToHashSet(StringComparer.Ordinal);
        var root = Path.Combine(RepoRoot(), "src", "Parlotype.Desktop");
        var problems = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match match in TrUsagePattern.Matches(File.ReadAllText(path)))
            {
                var key = match.Groups[1].Value;
                if (!keys.Contains(key))
                {
                    problems.Add(
                        $"{Path.GetRelativePath(RepoRoot(), path).Replace(Path.DirectorySeparatorChar, '/')} "
                        + $"uses {{loc:Tr {key}}} but that key is not in Strings.resx.");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void NoNewHardcodedUiText_InAxaml()
    {
        // Mirrors check 3 of scripts/check-localization.ps1 (which the Claude Code
        // hooks call); both read the same baseline, so the thing most likely to
        // drift cannot. Counts may shrink — then run the script with
        // -UpdateBaseline — but never grow, and a file at zero must leave the list.
        var baseline = LoadBaseline();
        var counts = ScanForLiterals(baseline.AllowedValues);
        var problems = new List<string>();

        foreach (var (file, count) in counts)
        {
            if (!baseline.PendingFiles.TryGetValue(file, out var allowed))
            {
                problems.Add($"{file} has {count} hardcoded UI literal(s). Move the copy into Strings.resx and bind it with {{loc:Tr Key}}.");
            }
            else if (count > allowed)
            {
                problems.Add($"{file} has {count} hardcoded UI literal(s), up from {allowed}. New copy must go through Strings.resx.");
            }
        }

        foreach (var (file, allowed) in baseline.PendingFiles)
        {
            var now = counts.GetValueOrDefault(file, 0);
            if (now < allowed)
                problems.Add($"{file} is down to {now} from {allowed} — run: pwsh scripts/check-localization.ps1 -UpdateBaseline");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private sealed record Baseline(string[] AllowedValues, Dictionary<string, int> PendingFiles);

    private static Baseline LoadBaseline()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "localization-baseline.json")));

        var allowed = document.RootElement.GetProperty("allowedValues")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        var pending = document.RootElement.GetProperty("pendingFiles")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);

        return new Baseline(allowed, pending);
    }

    /// <summary>
    /// Attributes that put words on screen. PlaceholderText, ToolTipText and the
    /// ToggleSwitch pair are included because they are easy to forget and just as
    /// visible as Text — and note that <c>\b</c> means plain <c>Content</c> does
    /// not match inside <c>OnContent</c> or <c>SizeToContent</c>, so each one has
    /// to be listed on its own.
    /// </summary>
    private static readonly Regex LiteralPattern = new(
        @"\b(Text|Content|OnContent|OffContent|Header|Watermark|PlaceholderText|ToolTip\.Tip|ToolTipText|Title)\s*=\s*""([^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex TrUsagePattern =
        new(@"\{\s*loc:Tr\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

    private static readonly Regex NumericEntityPattern =
        new("&#x?[0-9A-Fa-f]+;", RegexOptions.Compiled);

    /// <summary>
    /// Copy written as element <em>content</em> rather than an attribute —
    /// <c>&lt;TextBlock&gt;some words&lt;/TextBlock&gt;</c>. Invisible to
    /// <see cref="LiteralPattern"/>, which is how two untranslated paragraphs
    /// survived the whole extraction and were only caught by looking at a
    /// rendered screenshot.
    /// </summary>
    private static readonly Regex ContentTextPattern = new(
        @"<(TextBlock|Button|Run|TextBox|RadioButton|CheckBox|Expander)\b[^>]*?>(?!\s*<)(.*?)</\1>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static Dictionary<string, int> ScanForLiterals(string[] allowedValues)
    {
        var root = Path.Combine(RepoRoot(), "src", "Parlotype.Desktop");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var hits = 0;
            var text = File.ReadAllText(path);

            foreach (Match match in LiteralPattern.Matches(text))
            {
                var value = match.Groups[2].Value.Trim();
                if (value.Length == 0) continue;
                if (value.StartsWith('{')) continue;                    // a binding, incl. {loc:Tr}

                // Numeric character references are glyphs, but the 'x' in
                // &#x2715; would otherwise read as a letter and count the glyph
                // as translatable copy.
                var bare = NumericEntityPattern.Replace(value, string.Empty);
                if (!bare.Any(char.IsLetter)) continue;                 // glyphs: ✕ → ✓ •
                if (allowedValues.Contains(value, StringComparer.Ordinal)) continue;
                hits++;
            }

            foreach (Match match in ContentTextPattern.Matches(text))
            {
                var body = match.Groups[2].Value.Trim();
                if (body.Length == 0 || body.StartsWith('<') || body.StartsWith('{')) continue;
                if (!NumericEntityPattern.Replace(body, string.Empty).Any(char.IsLetter)) continue;
                if (allowedValues.Contains(body, StringComparer.Ordinal)) continue;
                hits++;
            }

            if (hits > 0)
            {
                var relative = Path.GetRelativePath(RepoRoot(), path).Replace(Path.DirectorySeparatorChar, '/');
                counts[relative] = hits;
            }
        }

        return counts;
    }

    private static string NeutralPath() => Path.Combine(ResourcesDirectory, "Strings.resx");

    private static string SatellitePath(string culture) =>
        Path.Combine(ResourcesDirectory, $"Strings.{culture}.resx");

    private static Dictionary<string, string> ReadResx(string path) =>
        XDocument.Load(path)
            .Root!
            .Elements("data")
            .Where(e => e.Attribute("name") is not null)
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? string.Empty);

    private static string Placeholders(string value) =>
        string.Join(
            ',',
            Regex.Matches(value, @"\{(\d+)")
                .Select(m => int.Parse(m.Groups[1].Value))
                .Distinct()
                .Order());

    /// <summary>
    /// Walks up from the test assembly to the directory holding Parlotype.slnx.
    /// The resx files are not copied to the output directory — the point is to
    /// check the sources that get committed, not a build artefact.
    /// </summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parlotype.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
