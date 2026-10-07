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
    public static async Task<string> GetRepositoryRootAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        // Show the absolute path of the top-level directory of the working tree.
        // If there is no working tree, report an error.
        string repositoryRoot = await RunGitCommandAsync(workingDirectory: workingDirectory, cancellationToken, "rev-parse", "--show-toplevel");

        return Path.GetFullPath(repositoryRoot.Trim());
    }

    /// <summary>
    /// Throws a <see cref="GitException"/> if <paramref name="revision"/> does not name a commit.
    /// </summary>
    public static async Task VerifyRevisionAsync(string workingDirectory, string revision, CancellationToken cancellationToken)
    {
        // Safely checks whether the reference or short hash "revision" points to a valid commit object (or can be dereferenced to one),
        //   printing the full commit hash if it exists and failing silently if it does not.
        // * rev-parse: Parse git revision (branch, tag, commit hash, etc.) and output the corresponding commit hash.
        // * --verify: Exit with a non-zero status if the revision is not valid.
        // * --quiet: Don't print an error message if the revision is not valid. A valid revision's hash is still printed,
        //   but we ignore it; we only care about the exit code.
        // * revision^{commit}: Dereference the revision to a commit object. If the revision is a tag, this will resolve it to the
        //   commit it points to.
        CallGitResult callGitResult = await CallGitExecutableAsync(workingDirectory: workingDirectory, cancellationToken, "rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}");

        if (callGitResult.ExitCode != 0)
        {
            throw new GitException($"'{revision}' is not a valid revision.");
        }
    }

    /// <summary>
    /// Returns the contents of the file at the given revision, or null if it did not exist at that revision.
    /// </summary>
    public static Task<string?> TryReadFileAtRevisionAsync(string filePath, string revision, CancellationToken cancellationToken)
    {
        return TryReadObjectAsync(filePath: filePath, revisionPrefix: $"{revision}:", cancellationToken);
    }

    /// <summary>
    /// Returns the staged contents of the file, or null if it is not in the index.
    /// </summary>
    public static Task<string?> TryReadStagedFileAsync(string filePath, CancellationToken cancellationToken)
    {
        return TryReadObjectAsync(filePath: filePath, revisionPrefix: ":", cancellationToken);
    }


    //
    // Private methods
    //

    private static async Task<string?> TryReadObjectAsync(string filePath, string revisionPrefix, CancellationToken cancellationToken)
    {
        // A path starting with ./ is resolved relative to the current directory, so run git from the file's
        //   directory rather than working out its path relative to the repository root.
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        string objectName = $"{revisionPrefix}./{Path.GetFileName(fullPath)}";

        // Check if the git object exists before trying to read it. If it doesn't, git show exits with a non-zero
        //   code and RunGitCommandAsync throws, but we want to return null instead.
        CallGitResult callGitResult = await CallGitExecutableAsync(directory, cancellationToken, "cat-file", "-e", objectName);
        bool gitObjectExists = callGitResult.ExitCode == 0;

        return gitObjectExists
            ? await RunGitCommandAsync(workingDirectory: directory, cancellationToken, "show", objectName)
            : null;
    }

    /// <summary>
    /// Run a git command from the command line. If the git command fails, throw a <see cref="GitException"/> with the 
    /// error message from git.
    /// </summary>
    /// <exception cref="GitException"></exception>
    private static async Task<string> RunGitCommandAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        CallGitResult callGitResult = await CallGitExecutableAsync(workingDirectory, cancellationToken, arguments);

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
    /// Execute a git command using <see cref="Process"/> and return the exit code, stdout, and stderr. If
    /// <paramref name="cancellationToken"/> is cancelled, kill git and throw <see cref="OperationCanceledException"/>.
    /// </summary>
    private static async Task<CallGitResult> CallGitExecutableAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
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

        try
        {
            // Start both reads before awaiting either, so that stderr keeps draining while we wait on stdout, and
            //   neither pipe can fill up and block git.
            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            string standardOutput = await standardOutputTask;
            string standardError = await standardErrorTask;
            await process.WaitForExitAsync(cancellationToken);

            return new CallGitResult(
                ExitCode: process.ExitCode,
                StandardOutput: standardOutput,
                StandardError: standardError);
        }
        catch (OperationCanceledException)
        {
            // Cancelling the wait doesn't stop git, so kill it rather than leave it running.
            process.Kill(entireProcessTree: true);
            throw;
        }
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
