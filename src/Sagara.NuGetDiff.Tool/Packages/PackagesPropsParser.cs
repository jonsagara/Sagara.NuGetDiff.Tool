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
    public static IReadOnlyList<PackageVersionEntry> Parse(string? dirPackagesPropsXml)
    {
        if (string.IsNullOrWhiteSpace(dirPackagesPropsXml))
        {
            return [];
        }

        // \uFEFF -> the byte order mark (BOM, ZERO WIDTH NO-BREAK SPACE). It's invisible, so it can't be shown here.
        XElement root = XDocument.Parse(dirPackagesPropsXml.TrimStart('\uFEFF')).Root
            ?? throw new InvalidDataException("The file has no root element.");

        // MSBuild evaluates every property before any item, so a version can reference a property declared
        //   anywhere in the file. Property conditions are ignored; the last definition wins.
        Dictionary<string, string> propertiesByLocalName = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement propertyElement in GetChildElements(root, "PropertyGroup").SelectMany(g => g.Elements()))
        {
            propertiesByLocalName[propertyElement.Name.LocalName] = ExpandProperties(propertyElement.Value.Trim(), propertiesByLocalName);
        }

        // Loop through each <ItemGroup> element.
        List<PackageVersionEntry> entries = [];
        foreach (XElement itemGroupElements in GetChildElements(root, "ItemGroup"))
        {
            string? groupCondition = NormalizeCondition(itemGroupElements.Attribute("Condition")?.Value);

            // Loop through each <PackageVersion> or <GlobalPackageReference> element.
            foreach (XElement itemElement in itemGroupElements.Elements().Where(e => PackageItemNames.Contains(e.Name.LocalName)))
            {
                // The package ID can be in the Include or Update attribute, and the version can be in the Version attribute or a child <Version> element.
                string? id = (itemElement.Attribute("Include") ?? itemElement.Attribute("Update"))?.Value.Trim();
                string? version = itemElement.Attribute("Version")?.Value ?? GetChildElements(itemElement, "Version").FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(id) || version is null)
                {
                    // Skip elements that don't have a valid ID or version.
                    continue;
                }

                string? condition = CombineConditions(groupCondition: groupCondition, itemCondition: NormalizeCondition(itemElement.Attribute("Condition")?.Value));

                entries.Add(new PackageVersionEntry(Id: id, Condition: condition, Version: ExpandProperties(version.Trim(), propertiesByLocalName)));
            }
        }

        return entries;
    }


    //
    // Private methods
    //

    private static IEnumerable<XElement> GetChildElements(XElement parentElement, string localName)
    {
        // Match on the local name so that files using the legacy MSBuild XML namespace also work.
        return parentElement
            .Elements()
            .Where(e => e.Name.LocalName == localName);
    }

    /// <summary>
    /// Replaces $(Name) with the property's value. Properties not defined in this file (e.g., from an imported
    /// file) are left as-is so that a change to the reference itself still shows up in the diff.
    /// </summary>
    private static string ExpandProperties(string value, Dictionary<string, string> properties)
    {
        return PropertyReferenceRegex.Replace(value, match => properties.TryGetValue(match.Groups["name"].Value, out string? propertyValue)
            ? propertyValue
            : match.Value);
    }

    /// <summary>
    /// Replaces multiple whitespace characters with a single space and trims the string. Returns null if the result is empty.
    /// </summary>
    private static string? NormalizeCondition(string? condition)
    {
        return string.IsNullOrWhiteSpace(condition)
            ? null
            : WhitespaceRegex.Replace(condition.Trim(), " ");
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
    private static partial Regex PropertyReferenceRegex { get; }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex { get; }
}
