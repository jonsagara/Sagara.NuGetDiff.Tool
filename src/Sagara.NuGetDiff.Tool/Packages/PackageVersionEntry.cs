namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// A single PackageVersion or GlobalPackageReference item from Directory.Packages.props.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Condition">The MSBuild condition that applies to the item, or null if it is unconditional.</param>
/// <param name="Version">The version, with any $(Property) references defined in the same file expanded.</param>
internal sealed record PackageVersionEntry(string Id, string? Condition, string Version);
