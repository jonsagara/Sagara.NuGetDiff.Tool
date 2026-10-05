namespace Sagara.NuGetDiff.Tool.CommitMessages;

/// <summary>
/// The shell that the generated git command will be pasted into.
/// </summary>
public enum ShellKind
{
    PowerShell,
    Posix,
}

/// <summary>
/// Quotes command arguments so that they are passed to git verbatim, including embedded newlines.
/// </summary>
internal static class ShellQuoter
{
    // PowerShell treats the typographic single quotes as quote characters, too.
    private static readonly char[] PowerShellSingleQuotes = ['\'', '‘', '’', '‚', '‛'];

    /// <summary>
    /// PowerShell on Windows, unless running under Git Bash/MSYS2 (which sets MSYSTEM); POSIX everywhere else.
    /// </summary>
    public static ShellKind Detect()
    {
        return OperatingSystem.IsWindows() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSYSTEM"))
            ? ShellKind.PowerShell
            : ShellKind.Posix;
    }

    public static string Quote(string value, ShellKind shell)
    {
        return shell switch
        {
            // Inside single quotes nothing is special except the quote itself, which is escaped by doubling it.
            ShellKind.PowerShell => $"'{string.Concat(value.Select(c => PowerShellSingleQuotes.Contains(c) ? $"{c}{c}" : c.ToString()))}'",

            // Inside single quotes nothing is special, and a quote can't be escaped, so close, emit \', and reopen.
            ShellKind.Posix => $"'{value.Replace("'", @"'\''", StringComparison.Ordinal)}'",

            _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, null),
        };
    }
}
