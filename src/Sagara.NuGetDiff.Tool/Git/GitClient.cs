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
        return Path.GetFullPath(Run(workingDirectory, "rev-parse", "--show-toplevel").Trim());
    }

    /// <summary>
    /// Throws a <see cref="GitException"/> if <paramref name="revision"/> does not name a commit.
    /// </summary>
    public static void VerifyRevision(string workingDirectory, string revision)
    {
        if (Execute(workingDirectory, "rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}").ExitCode != 0)
        {
            throw new GitException($"'{revision}' is not a valid revision.");
        }
    }

    /// <summary>
    /// Returns the contents of the file at the given revision, or null if it did not exist at that revision.
    /// </summary>
    public static string? TryReadFileAtRevision(string filePath, string revision)
    {
        return TryReadObject(filePath, $"{revision}:");
    }

    /// <summary>
    /// Returns the staged contents of the file, or null if it is not in the index.
    /// </summary>
    public static string? TryReadStagedFile(string filePath)
    {
        return TryReadObject(filePath, ":");
    }

    private static string? TryReadObject(string filePath, string revisionPrefix)
    {
        // A path starting with ./ is resolved relative to the current directory, so run git from the file's
        //   directory rather than working out its path relative to the repository root.
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        string objectName = $"{revisionPrefix}./{Path.GetFileName(fullPath)}";

        return Execute(directory, "cat-file", "-e", objectName).ExitCode == 0
            ? Run(directory, "show", objectName)
            : null;
    }

    private static string Run(string workingDirectory, params string[] arguments)
    {
        (int exitCode, string standardOutput, string standardError) = Execute(workingDirectory, arguments);

        if (exitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(standardError) ? $"exit code {exitCode}" : standardError.Trim();
            throw new GitException($"git {string.Join(' ', arguments)} failed: {detail}");
        }

        return standardOutput;
    }

    private static (int ExitCode, string StandardOutput, string StandardError) Execute(string workingDirectory, params string[] arguments)
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

        return (process.ExitCode, standardOutput, standardError);
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
