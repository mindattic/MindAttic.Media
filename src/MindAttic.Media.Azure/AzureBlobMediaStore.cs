using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MindAttic.Media.Azure;

public sealed class AzureBlobMediaStore<TContext> : IMediaStore where TContext : DbContext
{
    readonly TContext context;
    readonly AzureMediaOptions options;
    readonly AzureBlobContainerFactory containers;

    public AzureBlobMediaStore(TContext context, IOptions<AzureMediaOptions> options, AzureBlobContainerFactory containers)
    {
        this.context = context;
        this.options = options.Value;
        this.containers = containers;
    }

    public async Task<MediaItem> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        int? tenantId = null,
        string folder = "",
        string mediaType = "",
        int? width = null,
        int? height = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        var uid = Guid.NewGuid();
        var safeName = MediaNaming.SanitizeFileName(fileName);
        var now = DateTime.UtcNow;

        var blob = containers.Container.GetBlobClient(AzureBlobNaming.BlobNameFor(uid, safeName));

        // OpenWriteAsync + our own copy loop, rather than UploadAsync(stream): it guarantees a single
        // sequential pass over the source, so the hash we compute in flight is the hash of what landed,
        // and a multi-gigabyte video never sits in memory.
        string sha256;
        long size;
        var writeOptions = new BlobOpenWriteOptions
        {
            BufferSize = options.UploadBlockSizeBytes,
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await using (var destination = await blob.OpenWriteAsync(overwrite: true, writeOptions, ct))
        {
            (sha256, size) = await MediaStreams.CopyAndHashAsync(content, destination, options.CopyBufferSize, ct);
        }

        var item = new MediaItem
        {
            Uid = uid,
            TenantId = tenantId,
            MediaType = mediaType,
            Folder = folder,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = size,
            Sha256 = sha256,
            Bytes = null,
            BlobUri = blob.Uri.ToString(),
            Width = width,
            Height = height,
            Notes = notes,
            CreatedUtc = now,
            ModifiedUtc = now
        };

        context.Set<MediaItem>().Add(item);
        await context.SaveChangesAsync(ct);
        return item;
    }

    public async Task<IReadOnlyList<MediaItem>> ListAsync(
        int? tenantId = null,
        string? folder = null,
        string? mediaType = null,
        CancellationToken ct = default)
    {
        var q = context.Set<MediaItem>().Where(m => !m.IsDeleted);

        if (tenantId.HasValue)
            q = q.Where(m => m.TenantId == tenantId.Value);
        if (folder != null)
            q = q.Where(m => m.Folder == folder);
        if (mediaType != null)
            q = q.Where(m => m.MediaType == mediaType);

        return await q.OrderByDescending(m => m.CreatedUtc).ToListAsync(ct);
    }

    public Task<MediaItem?> GetMetaAsync(Guid uid, CancellationToken ct = default) =>
        context.Set<MediaItem>().FirstOrDefaultAsync(m => m.Uid == uid && !m.IsDeleted, ct);

    public async Task<(MediaItem Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default)
    {
        var item = await GetMetaAsync(uid, ct);

        if (item == null) return null;

        if (item.Bytes != null)
            return (item, new MemoryStream(item.Bytes, writable: false));

        if (item.BlobUri == null) return null;

        // Resolve through the configured container rather than re-authenticating the stored URI: a
        // connection-string deployment has no DefaultAzureCredential to fall back on.
        var blob = containers.Container.GetBlobClient(AzureBlobNaming.BlobNameFor(item));
        var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return (item, download.Value.Content);
    }

    /// <summary>
    /// Soft-delete only (HOUSE-LAW-2): the row is marked deleted and the blob is left in place, so a
    /// mistaken delete stays recoverable. Reclaiming storage is a deliberate, separate operation.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default)
    {
        var item = await GetMetaAsync(uid, ct);

        if (item == null) return false;

        item.IsDeleted = true;
        item.DeletedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}
