namespace MindAttic.Media;

public class MediaStoreOptions
{
    public string MediaRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "media");

    /// <summary>Payloads at or below this size are stored inline in the database row.</summary>
    public long InlineThresholdBytes { get; set; } = 2L * 1024 * 1024;

    /// <summary>How long a URL minted by an <see cref="IMediaUrlSigner"/> stays valid.</summary>
    public TimeSpan SignedUrlLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>max-age, in seconds, on a streamed <c>/_media/{uid}</c> response. Zero disables caching.</summary>
    public int CacheMaxAgeSeconds { get; set; } = 3600;

    public int CopyBufferSize { get; set; } = MediaStreams.DefaultCopyBufferSize;
}
