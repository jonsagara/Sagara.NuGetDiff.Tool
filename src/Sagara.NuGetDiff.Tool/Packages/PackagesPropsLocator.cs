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
        // Get the full path of the start directory.
        string startDirFullPath = Path.GetFullPath(startDirectory);

        // Get the full path of the repository root and trim any trailing directory separator to ensure
        //   consistent comparison.
        string repoRootFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));

        for (DirectoryInfo? directory = new(startDirFullPath); directory is not null; directory = directory.Parent)
        {
            string candidateDirPackagesPropsFilePath = Path.Combine(directory.FullName, FileName);

            if (File.Exists(candidateDirPackagesPropsFilePath))
            {
                // Found a Directory.Packages.props file; return its path.
                return candidateDirPackagesPropsFilePath;
            }

            if (string.Equals(Path.TrimEndingDirectorySeparator(directory.FullName), repoRootFullPath, PathComparison))
            {
                // We've reached the repository root; stop searching.
                break;
            }
        }

        // No Directory.Packages.props file was found between the start directory and the repository root.
        return null;
    }
}
