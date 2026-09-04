using System.Buffers;
using System.Security.Cryptography;

namespace MindAttic.Media;

/// <summary>
/// Single-pass copy helpers. Media payloads can be hundreds of megabytes (video), so nothing here
/// ever materialises the whole payload: bytes are hashed as they flow to their destination, and the
/// source is read exactly once, sequentially — which is all an <c>IBrowserFile</c> or a request body
/// stream can offer.
/// </summary>
public static class MediaStreams
{
    public const int DefaultCopyBufferSize = 81920;

    public static async Task<(string Sha256Hex, long Length)> CopyAndHashAsync(
        Stream source,
        Stream destination,
        int bufferSize = DefaultCopyBufferSize,
        CancellationToken ct = default)
    {
        if (bufferSize <= 0) bufferSize = DefaultCopyBufferSize;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        long total = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, bufferSize), ct)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), total);
    }
}
