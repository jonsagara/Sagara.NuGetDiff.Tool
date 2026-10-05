using System.Diagnostics;
using Sagara.NuGetDiff.Tool.Git;
using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

/// <summary>
/// Runs the real git CLI against a temporary repository.
/// </summary>
public sealed class GitClientTests : IDisposable
{
    private readonly DirectoryInfo _repository = Directory.CreateTempSubdirectory("nuget-diff-tests-");
    private readonly string _propsPath;

    public GitClientTests()
    {
        _propsPath = Path.Combine(_repository.FullName, PackagesPropsLocator.FileName);
        Git("init", "--quiet");
    }

    [Fact]
    public void DiffsCommittedFileAgainstWorkingTreeAndIndex()
    {
        WriteProps(("Serilog", "4.2.0"), ("Newtonsoft.Json", "13.0.3"));
        Commit();

        WriteProps(("Serilog", "4.3.0"), ("Polly", "8.5.0"));
        Git("add", PackagesPropsLocator.FileName);
        WriteProps(("Serilog", "4.4.0"));

        PackageDiff staged = Diff(GitClient.TryReadFileAtRevision(_propsPath, "HEAD"), GitClient.TryReadStagedFile(_propsPath));
        PackageDiff workingTree = Diff(GitClient.TryReadFileAtRevision(_propsPath, "HEAD"), File.ReadAllText(_propsPath));

        Assert.Equal([new PackageChange("Serilog", null, "4.2.0", "4.3.0")], staged.Upgraded);
        Assert.Equal([new PackageChange("Polly", null, null, "8.5.0")], staged.Added);
        Assert.Equal([new PackageChange("Newtonsoft.Json", null, "13.0.3", null)], staged.Removed);

        Assert.Equal([new PackageChange("Serilog", null, "4.2.0", "4.4.0")], workingTree.Upgraded);
        Assert.Empty(workingTree.Added);
    }

    [Fact]
    public void TryReadFileAtRevision_FileNotInRevision_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_repository.FullName, "other.txt"), "x");
        Commit();
        WriteProps(("Serilog", "4.2.0"));

        Assert.Null(GitClient.TryReadFileAtRevision(_propsPath, "HEAD"));
    }

    [Fact]
    public void VerifyRevision_UnknownRevision_Throws()
    {
        WriteProps(("Serilog", "4.2.0"));
        Commit();

        GitClient.VerifyRevision(_repository.FullName, "HEAD");
        Assert.Throws<GitException>(() => GitClient.VerifyRevision(_repository.FullName, "no-such-branch"));
    }

    [Fact]
    public void Locator_FindsPropsFileInAncestorDirectory()
    {
        WriteProps(("Serilog", "4.2.0"));
        DirectoryInfo nested = _repository.CreateSubdirectory(Path.Combine("src", "App"));

        string repositoryRoot = GitClient.GetRepositoryRoot(nested.FullName);

        Assert.Equal(_propsPath, PackagesPropsLocator.Find(nested.FullName, repositoryRoot), ignoreCase: OperatingSystem.IsWindows());
    }

    public void Dispose()
    {
        // git makes its object files read-only, which Directory.Delete refuses to remove on Windows.
        foreach (FileInfo file in _repository.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        _repository.Delete(recursive: true);
    }

    private static PackageDiff Diff(string? oldXml, string? newXml)
    {
        return PackageDiffer.Diff(PackagesPropsParser.Parse(oldXml), PackagesPropsParser.Parse(newXml));
    }

    private void WriteProps(params (string Id, string Version)[] packages)
    {
        string items = string.Concat(packages.Select(p => $"""
                <PackageVersion Include="{p.Id}" Version="{p.Version}" />

            """));

        File.WriteAllText(_propsPath, $"""
            <Project>
              <ItemGroup>
            {items}  </ItemGroup>
            </Project>
            """);
    }

    private void Commit()
    {
        Git("add", "--all");
        Git("-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "Test");
    }

    private void Git(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = _repository.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        string standardError = standardErrorTask.GetAwaiter().GetResult();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {standardError}");
    }
}
