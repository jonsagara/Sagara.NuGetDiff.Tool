using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Sagara.NuGetDiff.Tool.Git;

/// <summary>
/// The git command failed, or git could not be run.
/// </summary>
internal sealed class GitException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Runs the git CLI.
/// </summary>
internal static class GitClient
{
    /// <summary>
    /// Returns the root of the working tree that contains <paramref name="workingDirectory"/>.
    /// </summary>
    public static string GetRepositoryRoot(string workingDirectory)
    {
        // Show the absolute path of the top-level directory of the working tree.
        // If there is no working tree, report an error.
        var repositoryRoot = RunGitCommand(workingDirectory: workingDirectory, "rev-parse", "--show-toplevel");

        return Path.GetFullPath(repositoryRoot.Trim());
    }

    /// <summary>
    /// Throws a <see cref="GitException"/> if <paramref name="revision"/> does not name a commit.
    /// </summary>
    public static void VerifyRevision(string workingDirectory, string revision)
    {
        // Safely checks whether the reference or short hash "revision" points to a valid commit object (or can be dereferenced to one),
        //   printing the full 40-character commit hash if it exists and failing silently if it does not.
        // * rev-parse: Parse git revision (branch, tag, commit hash, etc.) and output the corresponding commit hash.
        // * --verify: Exit with a non-zero status if the revision is not valid.
        // * --quiet: Suppress output; we only care about the exit code.
        // * revision^{commit}: Dereference the revision to a commit object. If the revision is a tag, this will resolve it to the
        //   commit it points to.
        var callGitResult = CallGitExecutable(workingDirectory: workingDirectory, "rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}");

        if (callGitResult.ExitCode != 0)
        {
            throw new GitException($"'{revision}' is not a valid revision.");
        }
    }

    /// <summary>
    /// Returns the contents of the file at the given revision, or null if it did not exist at that revision.
    /// </summary>
    public static string? TryReadFileAtRevision(string filePath, string revision)
    {
        return TryReadObject(filePath: filePath, revisionPrefix: $"{revision}:");
    }

    /// <summary>
    /// Returns the staged contents of the file, or null if it is not in the index.
    /// </summary>
    public static string? TryReadStagedFile(string filePath)
    {
        return TryReadObject(filePath: filePath, revisionPrefix: ":");
    }


    //
    // Private methods
    //

    private static string? TryReadObject(string filePath, string revisionPrefix)
    {
        // A path starting with ./ is resolved relative to the current directory, so run git from the file's
        //   directory rather than working out its path relative to the repository root.
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        string objectName = $"{revisionPrefix}./{Path.GetFileName(fullPath)}";

        // Check if the git object exists before trying to read it; git will throw an error if it doesn't,
        //   and we want to return null instead of throwing.
        var callGitResult = CallGitExecutable(directory, "cat-file", "-e", objectName);
        var gitObjectExists = callGitResult.ExitCode == 0;

        return gitObjectExists
            ? RunGitCommand(workingDirectory: directory, "show", objectName)
            : null;
    }

    /// <summary>
    /// Run a git command from the command line. If the git command fails, throw a <see cref="GitException"/> with the 
    /// error message from git.
    /// </summary>
    /// <exception cref="GitException"></exception>
    private static string RunGitCommand(string workingDirectory, params string[] arguments)
    {
        var callGitResult = CallGitExecutable(workingDirectory, arguments);

        if (callGitResult.ExitCode == 0)
        {
            // Success. Return whatever git wrote to stdout.
            return callGitResult.StandardOutput;
        }

        // The git command failed. If git wrote to stderr, use that as the error message; otherwise, use the exit code
        //   and throw.
        string detail = !string.IsNullOrWhiteSpace(callGitResult.StandardError)
            ? callGitResult.StandardError.Trim()
            : $"exit code {callGitResult.ExitCode}";

        throw new GitException($"git {string.Join(' ', arguments)} failed: {detail}");
    }


    private record CallGitResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// Execute a git command using <see cref="Process"/> and return the exit code, stdout, and stderr.
    /// </summary>
    private static CallGitResult CallGitExecutable(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Start(startInfo);

        // Read stderr concurrently so that neither pipe can fill up and block git.
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = standardErrorTask.GetAwaiter().GetResult();
        process.WaitForExit();

        return new CallGitResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutput,
            StandardError: standardError);
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new GitException("Could not start git.");
        }
        catch (Win32Exception ex)
        {
            throw new GitException("Could not run git. Make sure it is installed and on your PATH.", ex);
        }
    }
}
