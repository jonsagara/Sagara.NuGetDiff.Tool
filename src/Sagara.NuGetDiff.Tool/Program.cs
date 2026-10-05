using System.CommandLine;
using System.Xml;
using Sagara.NuGetDiff.Tool.CommitMessages;
using Sagara.NuGetDiff.Tool.Git;
using Sagara.NuGetDiff.Tool.Packages;
using TextCopy;

Option<FileInfo?> fileOption = new("--file", "-f")
{
    Description = $"The {PackagesPropsLocator.FileName} file to compare. Defaults to the nearest one at or above the current directory.",
};

Option<string> fromOption = new("--from")
{
    Description = "The revision to compare from.",
    DefaultValueFactory = _ => "HEAD",
};

Option<string?> toOption = new("--to")
{
    Description = "Compare to this revision instead of the working tree.",
};

Option<bool> stagedOption = new("--staged")
{
    Description = "Compare to the staged file instead of the working tree.",
};

Option<string> subjectOption = new("--subject", "-s")
{
    Description = "The first line of the commit message.",
    DefaultValueFactory = _ => CommitCommandBuilder.DefaultSubject,
};

Option<ShellKind> shellOption = new("--shell")
{
    Description = "The shell to quote the git command for.",
    DefaultValueFactory = _ => ShellQuoter.Detect(),
};

Option<bool> noClipboardOption = new("--no-clipboard")
{
    Description = "Print the git command without copying it to the clipboard.",
};

RootCommand rootCommand = new($"Builds a git commit command listing the NuGet packages upgraded, downgraded, added, and removed in {PackagesPropsLocator.FileName}, and copies it to the clipboard.")
{
    fileOption,
    fromOption,
    toOption,
    stagedOption,
    subjectOption,
    shellOption,
    noClipboardOption,
};

rootCommand.SetAction(parseResult => Run(
    parseResult.GetValue(fileOption),
    parseResult.GetValue(fromOption)!,
    parseResult.GetValue(toOption),
    parseResult.GetValue(stagedOption),
    parseResult.GetValue(subjectOption)!,
    parseResult.GetValue(shellOption),
    parseResult.GetValue(noClipboardOption)));

return rootCommand.Parse(args).Invoke();

static int Run(FileInfo? file, string from, string? to, bool staged, string subject, ShellKind shell, bool noClipboard)
{
    if (staged && to is not null)
    {
        return Fail("--staged and --to cannot be used together.");
    }

    try
    {
        string currentDirectory = Environment.CurrentDirectory;
        string repositoryRoot = GitClient.GetRepositoryRoot(currentDirectory);

        string? propsPath = file?.FullName ?? PackagesPropsLocator.Find(currentDirectory, repositoryRoot);
        if (propsPath is null)
        {
            return Fail($"Could not find {PackagesPropsLocator.FileName} between the current directory and the repository root. Use --file to specify it.");
        }

        GitClient.VerifyRevision(repositoryRoot, from);
        if (to is not null)
        {
            GitClient.VerifyRevision(repositoryRoot, to);
        }

        string? oldXml = GitClient.TryReadFileAtRevision(propsPath, from);
        string? newXml = staged ? GitClient.TryReadStagedFile(propsPath)
            : to is not null ? GitClient.TryReadFileAtRevision(propsPath, to)
            : File.Exists(propsPath) ? File.ReadAllText(propsPath)
            : null;

        if (oldXml is null && newXml is null)
        {
            return Fail($"{propsPath} exists in neither version being compared.");
        }

        PackageDiff diff = PackageDiffer.Diff(PackagesPropsParser.Parse(oldXml), PackagesPropsParser.Parse(newXml));
        if (diff.IsEmpty)
        {
            Console.Error.WriteLine("No package changes detected.");
            return 0;
        }

        // When comparing to the working tree, the change usually isn't staged yet, so commit just the props
        //   file. Otherwise, commit whatever is staged.
        string? pathspec = staged || to is not null
            ? null
            : Path.GetRelativePath(currentDirectory, propsPath).Replace('\\', '/');

        string command = CommitCommandBuilder.BuildGitCommand(CommitCommandBuilder.BuildParagraphs(diff, subject), pathspec, shell);
        Console.WriteLine(command);

        if (!noClipboard)
        {
            CopyToClipboard(command);
        }

        return 0;
    }
    catch (GitException ex)
    {
        return Fail(ex.Message);
    }
    catch (XmlException ex)
    {
        return Fail($"Could not parse {PackagesPropsLocator.FileName}: {ex.Message}");
    }
}

static void CopyToClipboard(string text)
{
    // The command has already been printed, so a missing clipboard (e.g., a headless Linux box without
    //   xclip or wl-copy) is only worth a warning.
    try
    {
        ClipboardService.SetText(text);
        Console.Error.WriteLine("Copied to clipboard.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"warning: Could not copy to the clipboard: {ex.Message}");
    }
}

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}
