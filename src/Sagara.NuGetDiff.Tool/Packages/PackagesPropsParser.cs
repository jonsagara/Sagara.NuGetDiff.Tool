using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// Reads the package versions declared in a Directory.Packages.props file.
/// </summary>
internal static partial class PackagesPropsParser
{
    private static readonly HashSet<string> PackageItemNames = new(StringComparer.Ordinal) { "PackageVersion", "GlobalPackageReference" };

    /// <summary>
    /// Parses the package versions out of the file contents. Null or empty contents (e.g., the file does
    /// not exist at a given revision) yield no packages.
    /// </summary>
    public static IReadOnlyList<PackageVersionEntry> Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        XElement root = XDocument.Parse(xml.TrimStart('﻿')).Root
            ?? throw new InvalidDataException("The file has no root element.");

        // MSBuild evaluates every property before any item, so a version can reference a property declared
        //   anywhere in the file. Property conditions are ignored; the last definition wins.
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement property in ChildElements(root, "PropertyGroup").SelectMany(g => g.Elements()))
        {
            properties[property.Name.LocalName] = ExpandProperties(property.Value.Trim(), properties);
        }

        List<PackageVersionEntry> entries = [];
        foreach (XElement itemGroup in ChildElements(root, "ItemGroup"))
        {
            string? groupCondition = NormalizeCondition(itemGroup.Attribute("Condition")?.Value);

            foreach (XElement item in itemGroup.Elements().Where(e => PackageItemNames.Contains(e.Name.LocalName)))
            {
                string? id = (item.Attribute("Include") ?? item.Attribute("Update"))?.Value.Trim();
                string? version = item.Attribute("Version")?.Value ?? ChildElements(item, "Version").FirstOrDefault()?.Value;
                if (string.IsNullOrEmpty(id) || version is null)
                {
                    continue;
                }

                string? condition = CombineConditions(groupCondition, NormalizeCondition(item.Attribute("Condition")?.Value));
                entries.Add(new PackageVersionEntry(id, condition, ExpandProperties(version.Trim(), properties)));
            }
        }

        return entries;
    }

    private static IEnumerable<XElement> ChildElements(XElement parent, string localName)
    {
        // Match on the local name so that files using the legacy MSBuild XML namespace also work.
        return parent.Elements().Where(e => e.Name.LocalName == localName);
    }

    /// <summary>
    /// Replaces $(Name) with the property's value. Properties not defined in this file (e.g., from an imported
    /// file) are left as-is so that a change to the reference itself still shows up in the diff.
    /// </summary>
    private static string ExpandProperties(string value, Dictionary<string, string> properties)
    {
        return PropertyReferenceRegex().Replace(value, match => properties.TryGetValue(match.Groups["name"].Value, out string? propertyValue)
            ? propertyValue
            : match.Value);
    }

    private static string? NormalizeCondition(string? condition)
    {
        return string.IsNullOrWhiteSpace(condition)
            ? null
            : WhitespaceRegex().Replace(condition.Trim(), " ");
    }

    private static string? CombineConditions(string? groupCondition, string? itemCondition)
    {
        if (groupCondition is null || itemCondition is null)
        {
            return groupCondition ?? itemCondition;
        }

        return $"{groupCondition} and {itemCondition}";
    }

    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_.\-]*)\)")]
    private static partial Regex PropertyReferenceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
