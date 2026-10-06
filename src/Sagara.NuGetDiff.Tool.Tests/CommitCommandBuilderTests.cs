using Sagara.NuGetDiff.Tool.CommitMessages;
using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

public class CommitCommandBuilderTests
{
    private static readonly PackageDiff SampleDiff = new(
        Upgraded: [new PackageChange("Microsoft.Extensions.Hosting", null, "10.0.0", "10.0.1"), new PackageChange("Serilog", null, "4.2.0", "4.3.0")],
        Downgraded: [new PackageChange("Polly", null, "8.5.0", "8.4.0")],
        Changed: [],
        Added: [new PackageChange("Humanizer", "'$(OS)' == 'Windows_NT'", null, "3.0.1")],
        Removed: [new PackageChange("Newtonsoft.Json", null, "13.0.3", null)]);

    [Fact]
    public void BuildParagraphs_ListsOnePackagePerLineAndOmitsEmptySections()
    {
        IReadOnlyList<string> paragraphs = CommitCommandBuilder.BuildParagraphs(SampleDiff, CommitCommandBuilder.DefaultSubject);

        Assert.Equal(
            [
                "Update NuGet packages",
                "Upgraded:\nMicrosoft.Extensions.Hosting 10.0.0 -> 10.0.1\nSerilog 4.2.0 -> 4.3.0",
                "Downgraded:\nPolly 8.5.0 -> 8.4.0",
                "Added:\nHumanizer 3.0.1 ['$(OS)' == 'Windows_NT']",
                "Removed:\nNewtonsoft.Json 13.0.3",
            ],
            paragraphs);
    }

    [Fact]
    public void BuildGitCommand_PowerShell()
    {
        string command = CommitCommandBuilder.BuildGitCommandText(["Subject", "Upgraded:\nA 1.0.0 -> 2.0.0"], "Directory.Packages.props", ShellKind.PowerShell);

        Assert.Equal("git commit -m 'Subject' -m 'Upgraded:\nA 1.0.0 -> 2.0.0' -- 'Directory.Packages.props'", command);
    }

    [Fact]
    public void BuildGitCommand_WithoutPathspec_OmitsSeparator()
    {
        string command = CommitCommandBuilder.BuildGitCommandText(["Subject"], null, ShellKind.Posix);

        Assert.Equal("git commit -m 'Subject'", command);
    }

    [Theory]
    [InlineData(ShellKind.PowerShell, "Bob's update", "'Bob''s update'")]
    // \u2019 -> ’ (RIGHT SINGLE QUOTATION MARK), which PowerShell also treats as a single quote.
    [InlineData(ShellKind.PowerShell, "Bob\u2019s update", "'Bob\u2019\u2019s update'")]
    [InlineData(ShellKind.PowerShell, "$(echo hi) `n", "'$(echo hi) `n'")]
    [InlineData(ShellKind.Posix, "Bob's update", @"'Bob'\''s update'")]
    [InlineData(ShellKind.Posix, "$(echo hi) `ls`", "'$(echo hi) `ls`'")]
    public void Quote_EscapesSingleQuotesOnly(ShellKind shell, string value, string expected)
    {
        Assert.Equal(expected, ShellQuoter.Quote(value, shell));
    }
}
