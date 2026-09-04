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

    /// <summary>
    /// The catalog row alone, with no payload fetched. Callers that only need to decide how to serve
    /// an item — redirect, 304, or stream — must use this rather than <see cref="GetAsync"/>, which
    /// would pull the whole blob down just to throw it away.
    /// </summary>
    Task<MediaItem?> GetMetaAsync(Guid uid, CancellationToken ct = default);

    Task<(MediaItem Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default);
}
