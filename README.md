# MindAttic.Media

Drop-in media storage for ASP.NET Core apps: one upload API, a catalog row per file, and a media endpoint that serves from local disk or hands large files straight to Azure Blob Storage.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-library-512BD4) ![EF Core SQL Server](https://img.shields.io/badge/EF%20Core-SQL%20Server-CC2927) ![Azure Blob Storage](https://img.shields.io/badge/Azure-Blob%20Storage-0078D4) ![Version 2.0.0](https://img.shields.io/badge/version-2.0.0-2f7a4f)

```text
 your app                      MindAttic.Media                         storage
 ─────────                     ───────────────                         ───────
 UploadAsync(stream, ...) ──>  IMediaStore ──> hash (SHA-256) in flight
                                  │
                                  ├─ LocalDiskMediaStore   <= 2 MB ──> inline bytes in the MediaItem row
                                  │                        >  2 MB ──> MediaRoot/{uid}/{file}
                                  └─ AzureBlobMediaStore   any size ──> container/{uid}/{file}
                                  │
                                  └─ MediaItem row (uid, tenant, folder, type, size, sha256, ...)

 GET /_media/{uid} ──> signer present? ──> 302 to SAS, public or CDN URL  (Range and seeking by Azure)
                       otherwise       ──> stream through the app (ETag, Last-Modified, Range)
```

Two NuGet packages: `MindAttic.Media` (the abstraction, the EF Core model, the local-disk store and the endpoint) and `MindAttic.Media.Azure` (the Azure Blob backend and URL signer).

## Why

- Add uploads to an app with two registration lines and one endpoint mapping.
- Swap local disk for Azure Blob Storage without touching the code that uploads or links to media.
- Serve video properly: with Azure, the browser gets a signed URL and the storage service handles Range requests, resuming and seeking, so the bytes never pass through your app.
- Stream gigabyte uploads without buffering them in memory; size and SHA-256 are computed in one pass.
- Recover from mistakes: deletes are soft, and the stored file stays where it is.
- Refer to every file by one stable GUID, so storage layout can always be rebuilt from the catalog row.

## Features

- **`IMediaStore`:** `UploadAsync`, `ListAsync` (by tenant, folder and media type), `GetMetaAsync` (catalog row only, no payload), `GetAsync` (row plus content stream) and `DeleteAsync`.
- **`MediaItem` catalog row** with uid, optional tenant id, media type, folder, file name, content type, size, SHA-256, inline bytes or blob URI, width, height, notes, soft-delete flag, timestamps, an `Extra` field and a row version. `MediaItemTypeConfiguration` maps it with a unique index on uid and indexes on hash and on tenant, folder and file name.
- **Local disk store:** payloads up to `InlineThresholdBytes` (2 MB by default) are kept inline in the database row; larger ones spill to `MediaRoot/{uid}/{file}`. `ThresholdSpillStream` makes that decision without knowing the length up front.
- **Azure Blob store:** streams every upload to `{uid:N}/{file}` in the container with configurable block size, creating the container if needed. Authenticates with a connection string or with `DefaultAzureCredential` against a blob service URI.
- **URL signer** (`IMediaUrlSigner`): a plain or CDN-fronted URL when the container allows public reads, a key-signed SAS with a connection string, or a user-delegation SAS with Entra credentials. Anything else falls back to streaming.
- **`/_media/{uid}` endpoint:** redirects to a signed URL when a signer is registered, redirects to a stored absolute http or https URL, and otherwise streams the content with range processing, an ETag from the SHA-256, Last-Modified and a private `Cache-Control` max-age. Images, text, video, audio and PDF are served inline; other types download with their file name.
- **File-name sanitizing** that strips path segments and invalid characters.

## Quick start

The packages are published to a local feed (`C:\LocalNuGet`, see `nuget.config`). Reference them from your web project:

```xml
<PackageReference Include="MindAttic.Media" Version="2.0.0" />
<PackageReference Include="MindAttic.Media.Azure" Version="2.0.0" />
```

Add the catalog table to your DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new MediaItemTypeConfiguration());
}
```

Register a store and map the endpoint:

```csharp
// Local disk (default MediaRoot is ./media next to the app)
builder.Services.AddMedia<AppDbContext>(o => o.MediaRoot = @"D:\Data\media");

// or Azure Blob Storage, replacing the local store and adding the URL signer
builder.Services.AddMediaAzure<AppDbContext>(o =>
{
    o.BlobServiceUri = new Uri("https://<account>.blob.core.windows.net");
    o.ContainerName = "media";
});

app.MapMediaEndpoints();
```

Upload and link:

```csharp
var item = await store.UploadAsync(file.OpenReadStream(), file.FileName, file.ContentType, folder: "covers");
var url = $"/_media/{item.Uid}";
```

Add an EF Core migration in your app for the `MediaItem` table. The column mapping uses `varbinary(max)` and `nvarchar(max)`, so it targets SQL Server.

## Configuration

`MediaStoreOptions` (local disk, and the base of the Azure options):

| Option | Default | Meaning |
|---|---|---|
| `MediaRoot` | `media` next to the app | Folder for spilled payloads |
| `InlineThresholdBytes` | 2 MB | Payloads at or below this size stay inline in the row |
| `SignedUrlLifetime` | 1 hour | How long a signed URL stays valid |
| `CacheMaxAgeSeconds` | 3600 | max-age on streamed responses; 0 disables caching |
| `CopyBufferSize` | library default | Buffer size for the copy-and-hash loop |

`AzureMediaOptions` adds:

| Option | Default | Meaning |
|---|---|---|
| `BlobServiceUri` | none | Account endpoint, authenticated with `DefaultAzureCredential` |
| `ConnectionString` | none | Takes precedence over the URI and enables key-signed SAS |
| `ContainerName` | `media` | Blob container |
| `PublicRead` | false | Container allows anonymous reads; the signer hands out plain URLs, which a CDN can cache |
| `PublicBaseUri` | none | CDN or custom-domain origin used in place of the blob endpoint |
| `UploadBlockSizeBytes` | 8 MB | Block size for streamed uploads |

## Testing

```powershell
dotnet test MindAttic.Media.slnx
```

The NUnit suite covers the local disk store, blob naming, the spill stream and the `/_media` endpoint, using the EF Core in-memory provider. The Azure integration tests run against the Azurite emulator and are skipped when it is not listening:

```bash
npx azurite --silent --location ./azurite --blobHost 127.0.0.1 --blobPort 10000
```

## Project layout

| Path | What it is |
|---|---|
| `src/MindAttic.Media` | `IMediaStore`, `IMediaUrlSigner`, `MediaItem` and its EF configuration, `LocalDiskMediaStore`, `ThresholdSpillStream`, the endpoint and DI extensions |
| `src/MindAttic.Media.Azure` | `AzureBlobMediaStore`, `AzureBlobUrlSigner`, container factory, naming, options and DI extensions |
| `src/MindAttic.Media.Tests` | NUnit tests, including the Azurite integration tests |

## Limitations

- No migrations ship with the library; the host app owns its schema.
- The column types in `MediaItemTypeConfiguration` are SQL Server specific.
- Deletes are soft only. Reclaiming storage from deleted items is a separate operation the library does not provide.
- The Azure store always writes to blob storage; the inline threshold applies to the local disk store.

## Documentation

- [AGENTS.md](AGENTS.md): entry point for AI agents working in this repo; it points at the shared MindAttic agent standard.

## License

This repository has no LICENSE file; all rights are reserved.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic).
