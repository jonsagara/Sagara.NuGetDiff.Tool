using Sagara.NuGetDiff.Tool.Packages;

namespace Sagara.NuGetDiff.Tool.Tests;

public class PackagesPropsParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_MissingContents_ReturnsNoPackages(string? xml)
    {
        Assert.Empty(PackagesPropsParser.Parse(xml));
    }

    [Fact]
    public void Parse_ReadsPackageVersionAndGlobalPackageReferenceItems()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.3.0" />
                <GlobalPackageReference Include="Nerdbank.GitVersioning" Version="3.7.115" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(
            [new PackageVersionEntry("Serilog", null, "4.3.0"), new PackageVersionEntry("Nerdbank.GitVersioning", null, "3.7.115")],
            entries);
    }

    [Fact]
    public void Parse_ReadsVersionFromChildElement()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog">
                  <Version> 4.3.0 </Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """);

        Assert.Equal([new PackageVersionEntry("Serilog", null, "4.3.0")], entries);
    }

    [Fact]
    public void Parse_ExpandsPropertiesDefinedInTheFile()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Microsoft.Extensions.Hosting" Version="$(ExtensionsVersion)" />
                <PackageVersion Include="Other" Version="$(UndefinedVersion)" />
              </ItemGroup>
              <PropertyGroup>
                <ExtensionsMajor>10</ExtensionsMajor>
                <ExtensionsVersion>$(ExtensionsMajor).0.1</ExtensionsVersion>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(
            [new PackageVersionEntry("Microsoft.Extensions.Hosting", null, "10.0.1"), new PackageVersionEntry("Other", null, "$(UndefinedVersion)")],
            entries);
    }

    [Fact]
    public void Parse_CombinesAndNormalizesConditions()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("""
            <Project>
              <ItemGroup Condition="'$(TargetFramework)'   ==  'net10.0'">
                <PackageVersion Include="A" Version="1.0.0" />
                <PackageVersion Include="B" Version="1.0.0" Condition="'$(OS)' == 'Windows_NT'" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(
            [
                new PackageVersionEntry("A", "'$(TargetFramework)' == 'net10.0'", "1.0.0"),
                new PackageVersionEntry("B", "'$(TargetFramework)' == 'net10.0' and '$(OS)' == 'Windows_NT'", "1.0.0"),
            ],
            entries);
    }

    [Fact]
    public void Parse_SupportsLegacyMSBuildNamespace()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.3.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal([new PackageVersionEntry("Serilog", null, "4.3.0")], entries);
    }

    [Fact]
    public void Parse_IgnoresLeadingByteOrderMark()
    {
        IReadOnlyList<PackageVersionEntry> entries = PackagesPropsParser.Parse("﻿<Project><ItemGroup><PackageVersion Include=\"A\" Version=\"1.0.0\" /></ItemGroup></Project>");

        Assert.Single(entries);
    }
}
