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
        Dictionary<(string Id, string Condition), PackageVersionEntry> oldByKey = ToDictionary(oldEntries);
        Dictionary<(string Id, string Condition), PackageVersionEntry> newByKey = ToDictionary(newEntries);

        List<PackageChange> upgraded = [];
        List<PackageChange> downgraded = [];
        List<PackageChange> changed = [];
        List<PackageChange> added = [];
        List<PackageChange> removed = [];

        foreach (KeyValuePair<(string Id, string Condition), PackageVersionEntry> pair in newByKey)
        {
            PackageVersionEntry newEntry = pair.Value;

            if (!oldByKey.TryGetValue(pair.Key, out PackageVersionEntry? oldEntry))
            {
                added.Add(new PackageChange(newEntry.Id, newEntry.Condition, OldVersion: null, newEntry.Version));
                continue;
            }

            List<PackageChange>? target = CompareVersions(oldEntry.Version, newEntry.Version) switch
            {
                VersionChange.Upgrade => upgraded,
                VersionChange.Downgrade => downgraded,
                VersionChange.Unordered => changed,
                _ => null,
            };

            target?.Add(new PackageChange(newEntry.Id, newEntry.Condition, oldEntry.Version, newEntry.Version));
        }

        foreach (KeyValuePair<(string Id, string Condition), PackageVersionEntry> pair in oldByKey)
        {
            if (!newByKey.ContainsKey(pair.Key))
            {
                removed.Add(new PackageChange(pair.Value.Id, pair.Value.Condition, pair.Value.Version, NewVersion: null));
            }
        }

        return new PackageDiff(Sort(upgraded), Sort(downgraded), Sort(changed), Sort(added), Sort(removed));
    }

    private static Dictionary<(string Id, string Condition), PackageVersionEntry> ToDictionary(IReadOnlyList<PackageVersionEntry> entries)
    {
        // NuGet package IDs are case-insensitive. If a package is declared twice with the same condition, the
        //   last declaration wins, as it does in MSBuild.
        Dictionary<(string Id, string Condition), PackageVersionEntry> byKey = [];
        foreach (PackageVersionEntry entry in entries)
        {
            byKey[(entry.Id.ToUpperInvariant(), entry.Condition ?? string.Empty)] = entry;
        }

        return byKey;
    }

    private static VersionChange CompareVersions(string oldVersion, string newVersion)
    {
        if (NuGetVersion.TryParse(oldVersion, out NuGetVersion? oldNuGetVersion) && NuGetVersion.TryParse(newVersion, out NuGetVersion? newNuGetVersion))
        {
            int comparison = VersionComparer.Default.Compare(oldNuGetVersion, newNuGetVersion);
            if (comparison != 0)
            {
                return comparison < 0 ? VersionChange.Upgrade : VersionChange.Downgrade;
            }

            // Equivalent versions written differently (1.0 vs. 1.0.0) are not a change, but different build metadata is.
            return VersionComparer.VersionReleaseMetadata.Equals(oldNuGetVersion, newNuGetVersion) ? VersionChange.None : VersionChange.Unordered;
        }

        return string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase) ? VersionChange.None : VersionChange.Unordered;
    }

    private static List<PackageChange> Sort(List<PackageChange> changes)
    {
        return [.. changes
            .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Condition, StringComparer.Ordinal)];
    }
}
