namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// Finds the Directory.Packages.props file that applies to a directory.
/// </summary>
internal static class PackagesPropsLocator
{
    public const string FileName = "Directory.Packages.props";

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> to <paramref name="repositoryRoot"/>, the same way
    /// MSBuild does, and returns the first Directory.Packages.props found, or null if there isn't one.
    /// </summary>
    public static string? Find(string startDirectory, string repositoryRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));

        for (DirectoryInfo? directory = new(Path.GetFullPath(startDirectory)); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (string.Equals(Path.TrimEndingDirectorySeparator(directory.FullName), root, PathComparison))
            {
                break;
            }
        }

        return null;
    }
}
