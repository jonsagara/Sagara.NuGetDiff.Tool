using System.Text;
using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.CommitMessages;

/// <summary>
/// Turns a <see cref="PackageDiff"/> into a commit message and a git commit command.
/// </summary>
internal static class CommitCommandBuilder
{
    public const string DefaultSubject = "Update NuGet packages";

    /// <summary>
    /// Returns the paragraphs of the commit message: the subject, followed by one paragraph per non-empty
    /// section, each listing one package per line.
    /// </summary>
    public static IReadOnlyList<string> BuildParagraphs(PackageDiff diff, string subject)
    {
        List<string> paragraphs = [subject];

        AddSection(paragraphs, "Upgraded", diff.Upgraded, c => $"{c.OldVersion} -> {c.NewVersion}");
        AddSection(paragraphs, "Downgraded", diff.Downgraded, c => $"{c.OldVersion} -> {c.NewVersion}");
        AddSection(paragraphs, "Changed", diff.Changed, c => $"{c.OldVersion} -> {c.NewVersion}");
        AddSection(paragraphs, "Added", diff.Added, c => c.NewVersion);
        AddSection(paragraphs, "Removed", diff.Removed, c => c.OldVersion);

        return paragraphs;
    }

    /// <summary>
    /// Builds a git commit command with one -m per paragraph. git joins -m values with a blank line, which
    /// reproduces the message exactly.
    /// </summary>
    /// <param name="paragraphs">The paragraphs from <see cref="BuildParagraphs"/>.</param>
    /// <param name="pathspec">If not null, commit only this path instead of whatever is staged.</param>
    /// <param name="shell">The shell to quote arguments for.</param>
    public static string BuildGitCommand(IReadOnlyList<string> paragraphs, string? pathspec, ShellKind shell)
    {
        StringBuilder command = new("git commit");

        foreach (string paragraph in paragraphs)
        {
            command.Append(" -m ").Append(ShellQuoter.Quote(paragraph, shell));
        }

        if (pathspec is not null)
        {
            command.Append(" -- ").Append(ShellQuoter.Quote(pathspec, shell));
        }

        return command.ToString();
    }

    private static void AddSection(List<string> paragraphs, string header, IReadOnlyList<PackageChange> changes, Func<PackageChange, string?> formatVersion)
    {
        if (changes.Count == 0)
        {
            return;
        }

        StringBuilder section = new($"{header}:");

        foreach (PackageChange change in changes)
        {
            section.Append('\n').Append(change.Id).Append(' ').Append(formatVersion(change));

            if (change.Condition is not null)
            {
                section.Append(" [").Append(change.Condition).Append(']');
            }
        }

        paragraphs.Add(section.ToString());
    }
}
