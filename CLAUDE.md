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

## Backends (V2)
`IMediaStore` has two implementations, chosen by the consuming app:
- `LocalDiskMediaStore<TContext>` (package `MindAttic.Media`) — inline in the row up to
  `InlineThresholdBytes`, on disk under `MediaRoot` past it.
- `AzureBlobMediaStore<TContext>` (package `MindAttic.Media.Azure`) — streams to Blob Storage, and
  registers `AzureBlobUrlSigner` so `/_media/{uid}` **302s to a short-lived SAS URL**. That redirect is
  what gives large media (video) working Range requests and seeking; the bytes never transit the app.

Rules that must hold in any new backend:
- **Never buffer a whole payload.** Use `MediaStreams.CopyAndHashAsync` (single sequential pass, hashes
  in flight) and `ThresholdSpillStream`. A browser upload stream is forward-only and has no `Length`.
- **`GetMetaAsync` must not fetch the payload.** Callers use it to decide redirect-vs-304-vs-stream.
- **`DeleteAsync` is soft** (HOUSE-LAW-2): mark the row, leave the bytes.

## Tests
`src/MindAttic.Media.Tests` (NUnit). The `Azurite` category runs against the emulator and **self-skips**
when it is not listening:
```
npx azurite --silent --location ./azurite --blobHost 127.0.0.1 --blobPort 10000
dotnet test MindAttic.Media.slnx
```
