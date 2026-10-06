using NuGet.Versioning;

namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// Compares two sets of package versions.
/// </summary>
internal static class PackageDiffer
{
    private enum VersionChange
    {
        None,
        Upgrade,
        Downgrade,
        Unordered,
    }

    public static PackageDiff Diff(IReadOnlyList<PackageVersionEntry> oldEntries, IReadOnlyList<PackageVersionEntry> newEntries)
    {
        ArgumentNullException.ThrowIfNull(oldEntries);
        ArgumentNullException.ThrowIfNull(newEntries);

        Dictionary<PackageIdAndCondition, PackageVersionEntry> oldEntriesByIdAndCondition = GroupEntriesByIdAndCondition(oldEntries);
        Dictionary<PackageIdAndCondition, PackageVersionEntry> newEntriesByIdAndCondition = GroupEntriesByIdAndCondition(newEntries);

        List<PackageChange> upgraded = [];
        List<PackageChange> downgraded = [];
        List<PackageChange> changed = [];
        List<PackageChange> added = [];
        List<PackageChange> removed = [];

        foreach (KeyValuePair<PackageIdAndCondition, PackageVersionEntry> newEntryKvp in newEntriesByIdAndCondition)
        {
            PackageVersionEntry newEntry = newEntryKvp.Value;

            if (!oldEntriesByIdAndCondition.TryGetValue(newEntryKvp.Key, out PackageVersionEntry? oldEntry))
            {
                // There is no old entry for this package; it is newly added to Directory.Packages.props.
                added.Add(new PackageChange(Id: newEntry.Id, Condition: newEntry.Condition, OldVersion: null, NewVersion: newEntry.Version));
                continue;
            }

            // We have both an old and new entry for this package; compare the versions to determine the type of change.
            VersionChange versionChange = CompareVersions(oldVersion: oldEntry.Version, newVersion: newEntry.Version);

            // Track upgrades, downgrades, and unordered changes. We don't track unchanged packages.
            List<PackageChange>? target = versionChange switch
            {
                VersionChange.Upgrade => upgraded,
                VersionChange.Downgrade => downgraded,
                VersionChange.Unordered => changed,
                _ => null,
            };

            target?.Add(new PackageChange(Id: newEntry.Id, Condition: newEntry.Condition, OldVersion: oldEntry.Version, NewVersion: newEntry.Version));
        }

        foreach (KeyValuePair<PackageIdAndCondition, PackageVersionEntry> oldEntryKvp in oldEntriesByIdAndCondition)
        {
            if (!newEntriesByIdAndCondition.ContainsKey(oldEntryKvp.Key))
            {
                // There is no new entry for this package; it has been removed from Directory.Packages.props.
                removed.Add(new PackageChange(Id: oldEntryKvp.Value.Id, Condition: oldEntryKvp.Value.Condition, OldVersion: oldEntryKvp.Value.Version, NewVersion: null));
            }
        }

        return new PackageDiff(
            Upgraded: OrderByIdThenCondition(upgraded),
            Downgraded: OrderByIdThenCondition(downgraded),
            Changed: OrderByIdThenCondition(changed),
            Added: OrderByIdThenCondition(added),
            Removed: OrderByIdThenCondition(removed));
    }


    //
    // Private methods
    //

    private static Dictionary<PackageIdAndCondition, PackageVersionEntry> GroupEntriesByIdAndCondition(IReadOnlyList<PackageVersionEntry> entries)
    {
        // NuGet package IDs are case-insensitive. If a package is declared twice with the same condition, the
        //   last declaration wins, as it does in MSBuild.
        Dictionary<PackageIdAndCondition, PackageVersionEntry> entriesByIdAndCondition = [];

        foreach (PackageVersionEntry entry in entries)
        {
            PackageIdAndCondition idAndConditionKey = new(
                Id: entry.Id.ToUpperInvariant(),
                Condition: entry.Condition ?? string.Empty);

            entriesByIdAndCondition[idAndConditionKey] = entry;
        }

        return entriesByIdAndCondition;
    }

    private static VersionChange CompareVersions(string oldVersion, string newVersion)
    {
        if (NuGetVersion.TryParse(oldVersion, out NuGetVersion? oldNuGetVersion) && NuGetVersion.TryParse(newVersion, out NuGetVersion? newNuGetVersion))
        {
            int comparison = VersionComparer.Default.Compare(oldNuGetVersion, newNuGetVersion);
            if (comparison != 0)
            {
                return comparison < 0
                    ? VersionChange.Upgrade
                    : VersionChange.Downgrade;
            }

            // Equivalent versions written differently (1.0 vs. 1.0.0) are not a change, but different build metadata is.
            return VersionComparer.VersionReleaseMetadata.Equals(oldNuGetVersion, newNuGetVersion)
                ? VersionChange.None
                : VersionChange.Unordered;
        }
        
        // If we can't parse the versions as NuGet versions, fall back to a case-insensitive string comparison.
        return string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase)
            ? VersionChange.None
            : VersionChange.Unordered;
    }

    private static List<PackageChange> OrderByIdThenCondition(List<PackageChange> changes)
    {
        return [.. changes
            .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Condition, StringComparer.Ordinal)];
    }


    //
    // Types
    //

    private readonly record struct PackageIdAndCondition(string Id, string Condition);
}
