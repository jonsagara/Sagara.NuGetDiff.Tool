using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

public class PackageDifferTests
{
    [Fact]
    public void Diff_ClassifiesEachKindOfChange()
    {
        PackageDiff diff = PackageDiffer.Diff(
            [
                Entry("Serilog", "4.2.0"),
                Entry("Polly", "8.5.0"),
                Entry("Ranged", "[1.0,2.0)"),
                Entry("Newtonsoft.Json", "13.0.3"),
                Entry("Unchanged", "1.0.0"),
            ],
            [
                Entry("Serilog", "4.3.0"),
                Entry("Polly", "8.4.0"),
                Entry("Ranged", "[1.0,3.0)"),
                Entry("Unchanged", "1.0.0"),
                Entry("Humanizer", "3.0.1"),
            ]);

        Assert.Equal([new PackageChange("Serilog", null, "4.2.0", "4.3.0")], diff.Upgraded);
        Assert.Equal([new PackageChange("Polly", null, "8.5.0", "8.4.0")], diff.Downgraded);
        Assert.Equal([new PackageChange("Ranged", null, "[1.0,2.0)", "[1.0,3.0)")], diff.Changed);
        Assert.Equal([new PackageChange("Humanizer", null, null, "3.0.1")], diff.Added);
        Assert.Equal([new PackageChange("Newtonsoft.Json", null, "13.0.3", null)], diff.Removed);
    }

    [Fact]
    public void Diff_IdenticalPackages_IsEmpty()
    {
        PackageDiff diff = PackageDiffer.Diff([Entry("A", "1.0.0")], [Entry("A", "1.0.0")]);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_PrereleaseToRelease_IsUpgrade()
    {
        PackageDiff diff = PackageDiffer.Diff([Entry("A", "2.0.0-rc.2")], [Entry("A", "2.0.0")]);

        Assert.Single(diff.Upgraded);
    }

    [Fact]
    public void Diff_EquivalentVersionsWrittenDifferently_IsNotAChange()
    {
        PackageDiff diff = PackageDiffer.Diff([Entry("A", "1.0")], [Entry("A", "1.0.0")]);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_PackageIdCaseChange_IsNotAddOrRemove()
    {
        PackageDiff diff = PackageDiffer.Diff([Entry("newtonsoft.json", "13.0.1")], [Entry("Newtonsoft.Json", "13.0.3")]);

        Assert.Equal([new PackageChange("Newtonsoft.Json", null, "13.0.1", "13.0.3")], diff.Upgraded);
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
    }

    [Fact]
    public void Diff_TracksConditionalVersionsSeparately()
    {
        const string net9 = "'$(TargetFramework)' == 'net9.0'";
        const string net10 = "'$(TargetFramework)' == 'net10.0'";

        PackageDiff diff = PackageDiffer.Diff(
            [new PackageVersionEntry("EF", net9, "9.0.1"), new PackageVersionEntry("EF", net10, "10.0.0")],
            [new PackageVersionEntry("EF", net9, "9.0.1"), new PackageVersionEntry("EF", net10, "10.0.1")]);

        Assert.Equal([new PackageChange("EF", net10, "10.0.0", "10.0.1")], diff.Upgraded);
    }

    [Fact]
    public void Diff_SortsByPackageId()
    {
        PackageDiff diff = PackageDiffer.Diff([], [Entry("zeta", "1.0.0"), Entry("Alpha", "1.0.0"), Entry("beta", "1.0.0")]);

        Assert.Equal(["Alpha", "beta", "zeta"], diff.Added.Select(c => c.Id));
    }

    private static PackageVersionEntry Entry(string id, string version)
    {
        return new PackageVersionEntry(id, null, version);
    }
}
