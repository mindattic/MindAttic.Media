namespace MindAttic.Media;

public interface IMediaStore
{
    Task<MediaItem> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        int? tenantId = null,
        string folder = "",
        string mediaType = "",
        int? width = null,
        int? height = null,
        string? notes = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<MediaItem>> ListAsync(
        int? tenantId = null,
        string? folder = null,
        string? mediaType = null,
        CancellationToken ct = default);

    Task<(MediaItem Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default);
}
