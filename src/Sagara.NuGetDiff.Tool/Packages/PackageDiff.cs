namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// A package whose version differs between two versions of Directory.Packages.props.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Condition">The MSBuild condition that applies to the item, or null if it is unconditional.</param>
/// <param name="OldVersion">The previous version, or null if the package was added.</param>
/// <param name="NewVersion">The new version, or null if the package was removed.</param>
internal sealed record PackageChange(string Id, string? Condition, string? OldVersion, string? NewVersion);

/// <summary>
/// The package changes between two versions of Directory.Packages.props. Each list is sorted by package ID.
/// </summary>
/// <param name="Upgraded">Packages whose version increased.</param>
/// <param name="Downgraded">Packages whose version decreased.</param>
/// <param name="Changed">Packages whose version changed but can't be ordered (e.g., a version range or an unresolved property).</param>
/// <param name="Added">Packages that are new.</param>
/// <param name="Removed">Packages that no longer exist.</param>
internal sealed record PackageDiff(
    IReadOnlyList<PackageChange> Upgraded,
    IReadOnlyList<PackageChange> Downgraded,
    IReadOnlyList<PackageChange> Changed,
    IReadOnlyList<PackageChange> Added,
    IReadOnlyList<PackageChange> Removed)
{
    public bool IsEmpty => Upgraded.Count == 0 && Downgraded.Count == 0 && Changed.Count == 0 && Added.Count == 0 && Removed.Count == 0;
}
