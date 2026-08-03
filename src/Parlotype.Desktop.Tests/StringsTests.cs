using System.Reflection;
using Parlotype.Desktop.Resources;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Every <see cref="Strings"/> accessor must resolve to real resx content —
/// the accessor falls back to the key name on a miss, so "value equals the
/// property name" is the signature of a property without a resx entry.
/// </summary>
public class StringsTests
{
    [Fact]
    public void EveryString_ResolvesToNonEmptyResxContent()
    {
        var properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var value = (string?)property.GetValue(null);
            Assert.False(string.IsNullOrWhiteSpace(value), $"'{property.Name}' is empty");
            Assert.False(value == property.Name, $"'{property.Name}' has no resx entry");
        }
    }
}
