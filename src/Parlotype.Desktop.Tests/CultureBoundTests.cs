using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Collection marker for tests that change the interface language.
/// </summary>
/// <remarks>
/// <para>
/// <c>Localizer.SetCulture</c> writes <see cref="System.Globalization.CultureInfo.DefaultThreadCurrentUICulture"/>,
/// which is process-wide: while one of these tests holds Russian, every other
/// test in the assembly sees Russian too. They therefore share one collection
/// with parallelization off, so they never overlap each other or run beside a
/// test that asserts on English copy.
/// </para>
/// <para>
/// The definition lives on this empty marker class rather than on one of the
/// test classes. With it attached to a test class, adding a <em>second</em>
/// class to the collection stopped serializing them and produced failures that
/// appeared only in a full-suite run — each class passed alone and both passed
/// together under a filter.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureBoundTests
{
    public const string Name = "culture-bound";
}
