using System.CommandLine;
using System.Xml;
using Sagara.NuGetDiff.Tool.CommitMessages;
using Sagara.NuGetDiff.Tool.Git;
using Sagara.NuGetDiff.Tool.Packages;
using TextCopy;


//
// Configure the various command line options and arguments, and construct the root command.
//

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

// Each option is added with C#'s goofy inferred-.Add() syntax.
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

rootCommand.SetAction(
    parseResult => 
        Run(
            file: parseResult.GetValue(fileOption),
            from: parseResult.GetValue(fromOption)!,
            to: parseResult.GetValue(toOption),
            staged: parseResult.GetValue(stagedOption),
            subject: parseResult.GetValue(subjectOption)!,
            shell: parseResult.GetValue(shellOption),
            noClipboard: parseResult.GetValue(noClipboardOption)));

return rootCommand.Parse(args).Invoke();


//
// Private methods
//

/// <summary>
/// Runs the main application logic.
/// </summary>
static int Run(FileInfo? file, string from, string? to, bool staged, string subject, ShellKind shell, bool noClipboard)
{
    if (staged && to is not null)
    {
        return Fail("--staged and --to cannot be used together.");
    }

    try
    {
        string currentDirectory = Environment.CurrentDirectory;
        string repositoryRoot = GitClient.GetRepositoryRoot(workingDirectory: currentDirectory);

        // Try to locate Directory.Packages.props file in the repository, starting at the current directory and
        //   walking up to the repository root. If the user specified a file, use that instead.
        string? dirPackagePropsFilePath = file?.FullName ?? PackagesPropsLocator.Find(startDirectory: currentDirectory, repositoryRoot: repositoryRoot);
        if (dirPackagePropsFilePath is null)
        {
            return Fail($"Could not find {PackagesPropsLocator.FileName} between the current directory and the repository root. Use --file to specify it.");
        }

        GitClient.VerifyRevision(workingDirectory: repositoryRoot, revision: from);
        if (to is not null)
        {
            GitClient.VerifyRevision(workingDirectory: repositoryRoot, revision: to);
        }

        string? oldDirPackagePropsFileXml = GitClient.TryReadFileAtRevision(dirPackagePropsFilePath, from);
        string? newDirPackagePropsFileXml = staged ? GitClient.TryReadStagedFile(dirPackagePropsFilePath)
            : to is not null ? GitClient.TryReadFileAtRevision(dirPackagePropsFilePath, to)
            : File.Exists(dirPackagePropsFilePath) ? File.ReadAllText(dirPackagePropsFilePath)
            : null;

        if (oldDirPackagePropsFileXml is null && newDirPackagePropsFileXml is null)
        {
            return Fail($"{dirPackagePropsFilePath} exists in neither version being compared.");
        }

        PackageDiff diff = PackageDiffer.Diff(oldEntries: PackagesPropsParser.Parse(oldDirPackagePropsFileXml), newEntries: PackagesPropsParser.Parse(newDirPackagePropsFileXml));
        if (diff.IsEmpty)
        {
            Console.Error.WriteLine("No package changes detected.");
            return 0;
        }

        // When comparing to the working tree, the change usually isn't staged yet, so commit just the props
        //   file. Otherwise, commit whatever is staged.
        string? pathspec = staged || to is not null
            ? null
            : Path.GetRelativePath(currentDirectory, dirPackagePropsFilePath).Replace('\\', '/');

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
