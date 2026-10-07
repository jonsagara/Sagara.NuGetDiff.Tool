using System.Diagnostics;
using Sagara.NuGetDiff.Tool.Git;
using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

/// <summary>
/// Runs the real git CLI against a temporary repository.
/// </summary>
public sealed class GitClientTests : IAsyncLifetime
{
    private readonly DirectoryInfo _repository = Directory.CreateTempSubdirectory("nuget-diff-tests-");
    private readonly string _propsPath;

    public GitClientTests()
    {
        _propsPath = Path.Combine(_repository.FullName, PackagesPropsLocator.FileName);
    }

    public async ValueTask InitializeAsync()
    {
        await GitAsync("init", "--quiet");
    }

    [Fact]
    public async Task DiffsCommittedFileAgainstWorkingTreeAndIndex()
    {
        WriteProps(("Serilog", "4.2.0"), ("Newtonsoft.Json", "13.0.3"));
        await CommitAsync();

        WriteProps(("Serilog", "4.3.0"), ("Polly", "8.5.0"));
        await GitAsync("add", PackagesPropsLocator.FileName);
        WriteProps(("Serilog", "4.4.0"));

        string? headXml = await GitClient.TryReadFileAtRevisionAsync(_propsPath, "HEAD", CancellationToken);
        PackageDiff staged = Diff(headXml, await GitClient.TryReadStagedFileAsync(_propsPath, CancellationToken));
        PackageDiff workingTree = Diff(headXml, File.ReadAllText(_propsPath));

        Assert.Equal([new PackageChange("Serilog", null, "4.2.0", "4.3.0")], staged.Upgraded);
        Assert.Equal([new PackageChange("Polly", null, null, "8.5.0")], staged.Added);
        Assert.Equal([new PackageChange("Newtonsoft.Json", null, "13.0.3", null)], staged.Removed);

        Assert.Equal([new PackageChange("Serilog", null, "4.2.0", "4.4.0")], workingTree.Upgraded);
        Assert.Empty(workingTree.Added);
    }

    [Fact]
    public async Task TryReadFileAtRevision_FileNotInRevision_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_repository.FullName, "other.txt"), "x");
        await CommitAsync();
        WriteProps(("Serilog", "4.2.0"));

        Assert.Null(await GitClient.TryReadFileAtRevisionAsync(_propsPath, "HEAD", CancellationToken));
    }

    [Fact]
    public async Task VerifyRevision_UnknownRevision_Throws()
    {
        WriteProps(("Serilog", "4.2.0"));
        await CommitAsync();

        await GitClient.VerifyRevisionAsync(_repository.FullName, "HEAD", CancellationToken);
        await Assert.ThrowsAsync<GitException>(() => GitClient.VerifyRevisionAsync(_repository.FullName, "no-such-branch", CancellationToken));
    }

    [Fact]
    public async Task Locator_FindsPropsFileInAncestorDirectory()
    {
        WriteProps(("Serilog", "4.2.0"));
        DirectoryInfo nested = _repository.CreateSubdirectory(Path.Combine("src", "App"));

        string repositoryRoot = await GitClient.GetRepositoryRootAsync(nested.FullName, CancellationToken);

        Assert.Equal(_propsPath, PackagesPropsLocator.Find(nested.FullName, repositoryRoot), ignoreCase: OperatingSystem.IsWindows());
    }

    public ValueTask DisposeAsync()
    {
        // git makes its object files read-only, which Directory.Delete refuses to remove on Windows.
        foreach (FileInfo file in _repository.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        _repository.Delete(recursive: true);

        return ValueTask.CompletedTask;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

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

    private async Task CommitAsync()
    {
        await GitAsync("add", "--all");
        await GitAsync("-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "Test");
    }

    private async Task GitAsync(params string[] arguments)
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
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken);
        await standardOutputTask;
        string standardError = await standardErrorTask;
        await process.WaitForExitAsync(CancellationToken);

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {standardError}");
    }
}
