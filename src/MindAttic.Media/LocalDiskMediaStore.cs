using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MindAttic.Media;

public sealed class LocalDiskMediaStore<TContext> : IMediaStore where TContext : DbContext
{
    readonly TContext context;
    readonly MediaStoreOptions options;

    public LocalDiskMediaStore(TContext context, IOptions<MediaStoreOptions> options)
    {
        this.context = context;
        this.options = options.Value;
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
        var path = Path.Combine(options.MediaRoot, uid.ToString("N"), safeName);

        byte[]? inlineBytes;
        string? blobUri;
        string sha256;
        long size;

        var sink = new ThresholdSpillStream(options.InlineThresholdBytes, () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return File.Create(path);
        });

        await using (sink)
        {
            (sha256, size) = await MediaStreams.CopyAndHashAsync(content, sink, options.CopyBufferSize, ct);
            inlineBytes = sink.InlineBytes;
            blobUri = sink.Spilled ? path : null;
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
            Bytes = inlineBytes,
            BlobUri = blobUri,
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

        if (item.BlobUri != null && File.Exists(item.BlobUri))
            return (item, File.OpenRead(item.BlobUri));

        return null;
    }

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
