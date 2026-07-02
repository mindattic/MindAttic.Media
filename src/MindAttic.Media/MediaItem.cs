namespace MindAttic.Media;

public sealed class MediaItem
{
    public int Id { get; set; }
    public Guid Uid { get; set; }
    public int? TenantId { get; set; }
    public string MediaType { get; set; } = "";
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public byte[]? Bytes { get; set; }
    public string? BlobUri { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string? Extra { get; set; }
    public byte[]? RowVersion { get; set; }
}
