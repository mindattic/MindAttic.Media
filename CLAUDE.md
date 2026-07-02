# MindAttic.Media Project Rules

## Versioning
Whole-number versioning only: `1.0.0`, `2.0.0`, `3.0.0`. Never semver minor/patch.

## Code Style
- No underscore-prefixed fields. Use `camelCase` for private fields.
- No comments explaining WHAT the code does. Only add a comment when WHY is non-obvious.
- No EF Core null-conditional (`?.`) inside expression-tree lambdas — fails CS8072.

## NuGet Feed
Local feed: `C:\LocalNuGet`. After packing, copy `.nupkg` to `C:\LocalNuGet`.
For StreetSamurai: also copy to `D:\Projects\MindAttic\StreetSamurai\lib\local-packages\`.

## Dual-Reference Debug Pattern
Callers reference the package for Release builds and the source project for Debug:
```xml
<PackageReference Include="MindAttic.Media" Version="1.0.0" />
<ProjectReference Include="..\MindAttic.Media\src\MindAttic.Media\MindAttic.Media.csproj"
                  Condition="'$(Configuration)' == 'Debug'" />
```
