namespace MindAttic.Media;

/// <summary>
/// Optional companion to <see cref="IMediaStore"/>: mints a URL the browser can fetch directly.
/// Registering one turns <c>/_media/{uid}</c> into a redirect, which is what makes large media
/// (video above all) usable — the storage service handles Range requests, resumes and seeking, and
/// the bytes never transit the app. A store with no signer streams through the app as before.
/// </summary>
public interface IMediaUrlSigner
{
    /// <summary>
    /// A directly-fetchable URL for <paramref name="item"/>, or null when this signer cannot serve it
    /// (an inline payload, a local-disk path, a misconfigured backend).
    /// </summary>
    ValueTask<Uri?> TryCreateReadUrlAsync(MediaItem item, TimeSpan lifetime, CancellationToken ct = default);
}
