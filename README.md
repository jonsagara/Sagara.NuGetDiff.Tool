# Sagara.NuGetDiff.Tool

A .NET global tool that compares your `Directory.Packages.props` with the last commit and builds a ready-to-run `git commit` command listing every package that was upgraded, downgraded, added, or removed. The command is printed and copied to the clipboard.

## Install

```
dotnet tool install -g Sagara.NuGetDiff.Tool
```

Or run it without installing:

```
dnx Sagara.NuGetDiff.Tool
```

## Usage

After updating packages, run `nuget-diff` from anywhere in the repository:

```
> nuget-diff
git commit -m 'Update NuGet packages' -m 'Upgraded:
Microsoft.Extensions.Hosting 10.0.0 -> 10.0.1
Serilog 4.2.0 -> 4.3.0' -m 'Downgraded:
Polly 8.5.0 -> 8.4.0' -m 'Added:
Humanizer 3.0.1' -m 'Removed:
Newtonsoft.Json 13.0.3' -- 'Directory.Packages.props'
Copied to clipboard.
```

Paste it to commit with this message:

```
Update NuGet packages

Upgraded:
Microsoft.Extensions.Hosting 10.0.0 -> 10.0.1
Serilog 4.2.0 -> 4.3.0

Downgraded:
Polly 8.5.0 -> 8.4.0

Added:
Humanizer 3.0.1

Removed:
Newtonsoft.Json 13.0.3
```

By default the tool compares `HEAD` with the working tree, and the command commits only `Directory.Packages.props`. With `--staged` or `--to`, the command has no pathspec, so it commits whatever is staged.

### Options

| Option | Description |
|---|---|
| `-f, --file <path>` | The `Directory.Packages.props` to compare. Defaults to the nearest one at or above the current directory, stopping at the repository root. |
| `--from <rev>` | The revision to compare from. Defaults to `HEAD`. |
| `--to <rev>` | Compare to this revision instead of the working tree. Useful for writing a message for an existing commit, e.g. `--from HEAD~1 --to HEAD`. |
| `--staged` | Compare to the staged file instead of the working tree. |
| `-s, --subject <text>` | The first line of the commit message. Defaults to `Update NuGet packages`. |
| `--shell <PowerShell\|Posix>` | The shell to quote the command for. Defaults to PowerShell on Windows (Posix under Git Bash) and Posix elsewhere. |
| `--no-clipboard` | Print the command without copying it. |

## Details

- Versions that reference a property defined in the same file, such as `Version="$(ExtensionsVersion)"`, are resolved, so changing one property lists every package that uses it. Property conditions are ignored, and the last definition wins.
- Packages declared under a `Condition` are tracked separately, and the condition is shown after the version.
- Versions are compared using NuGet's rules. A version that can't be ordered, such as a range or an unresolved property, is listed under `Changed`.
- Package IDs are case-insensitive, and equivalent versions (`1.0` vs. `1.0.0`) are not reported as changes.
- On Linux, copying to the clipboard requires `xclip`, `xsel`, or `wl-copy`. If the copy fails, the command is still printed.

## Building

```
dotnet build
dotnet test
dotnet pack src/Sagara.NuGetDiff.Tool -o artifacts
dotnet tool install -g Sagara.NuGetDiff.Tool --add-source ./artifacts --prerelease
```
