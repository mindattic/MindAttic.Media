using System.Security.Cryptography;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MindAttic.Media.Azure;

public sealed class AzureBlobMediaStore<TContext> : IMediaStore where TContext : DbContext
{
    readonly TContext context;
    readonly AzureMediaOptions options;
    readonly Lazy<BlobContainerClient> container;

    public AzureBlobMediaStore(TContext context, IOptions<AzureMediaOptions> options)
    {
        this.context = context;
        this.options = options.Value;
        container = new Lazy<BlobContainerClient>(BuildContainerClient);
    }

    BlobContainerClient BuildContainerClient()
    {
        BlobServiceClient service;

        if (!string.IsNullOrEmpty(options.ConnectionString))
            service = new BlobServiceClient(options.ConnectionString);
        else if (options.BlobServiceUri != null)
            service = new BlobServiceClient(options.BlobServiceUri, new DefaultAzureCredential());
        else
            throw new InvalidOperationException("AzureMediaOptions requires either ConnectionString or BlobServiceUri.");

        var client = service.GetBlobContainerClient(options.ContainerName);
        client.CreateIfNotExists();
        return client;
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
        var safeName = SanitizeFileName(fileName);
        var now = DateTime.UtcNow;

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var blobName = $"{uid:N}/{safeName}";
        ms.Position = 0;

        var blob = container.Value.GetBlobClient(blobName);
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);

        var item = new MediaItem
        {
            Uid = uid,
            TenantId = tenantId,
            MediaType = mediaType,
            Folder = folder,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = bytes.Length,
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

    public async Task<(MediaItem Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default)
    {
        var item = await context.Set<MediaItem>()
            .FirstOrDefaultAsync(m => m.Uid == uid && !m.IsDeleted, ct);

        if (item == null) return null;

        if (item.Bytes != null)
            return (item, new MemoryStream(item.Bytes));

        if (item.BlobUri != null)
        {
            var blob = new BlobClient(new Uri(item.BlobUri), new DefaultAzureCredential());
            var download = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return (item, download.Value.Content);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default)
    {
        var item = await context.Set<MediaItem>()
            .FirstOrDefaultAsync(m => m.Uid == uid && !m.IsDeleted, ct);

        if (item == null) return false;

        item.IsDeleted = true;
        item.DeletedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }
}
