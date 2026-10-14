using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

public class NumericOrdinalStringComparerTests
{
    [Theory]
    [InlineData("net8.0", "net10.0")]
    [InlineData("net9.0", "net10.0")]
    [InlineData("net10.0", "net10.1")]
    [InlineData("a2", "a10")]
    [InlineData("a", "a1")]
    [InlineData("a1", "a1b")]
    [InlineData("1a", "a")]
    [InlineData("A", "a")]
    [InlineData("net08.0", "net8.0")]
    [InlineData("v99999999999999999999", "v100000000000000000000")]
    [InlineData(null, "")]
    public void Compare_OrdersFirstBeforeSecond(string? first, string? second)
    {
        Assert.True(NumericOrdinalStringComparer.Instance.Compare(first, second) < 0);
        Assert.True(NumericOrdinalStringComparer.Instance.Compare(second, first) > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("'$(TargetFramework)' == 'net10.0'")]
    public void Compare_EqualStrings_ReturnsZero(string? value)
    {
        Assert.Equal(0, NumericOrdinalStringComparer.Instance.Compare(value, value is null ? null : new string(value)));
    }

    [Fact]
    public void Sort_OrdersTargetFrameworkConditionsNumerically()
    {
        string[] conditions = ["'$(TargetFramework)' == 'net10.0'", "'$(TargetFramework)' == 'net8.0'", "'$(TargetFramework)' == 'net9.0'"];

        Assert.Equal(
            ["'$(TargetFramework)' == 'net8.0'", "'$(TargetFramework)' == 'net9.0'", "'$(TargetFramework)' == 'net10.0'"],
            conditions.Order(NumericOrdinalStringComparer.Instance));
    }
}
