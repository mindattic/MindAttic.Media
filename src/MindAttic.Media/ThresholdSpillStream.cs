namespace MindAttic.Media;

/// <summary>
/// A write-only sink that keeps small payloads in memory and spills to an overflow stream the moment
/// they grow past <paramref name="threshold"/>. This is how a store decides inline-vs-blob without
/// knowing the length up front — a browser upload stream has no reliable <c>Length</c>, and buffering
/// a video to find out would defeat the point.
/// </summary>
public sealed class ThresholdSpillStream : Stream
{
    readonly long threshold;
    readonly Func<Stream> openOverflow;
    MemoryStream? buffered = new();
    Stream? overflow;
    long length;

    public ThresholdSpillStream(long threshold, Func<Stream> openOverflow)
    {
        this.threshold = threshold;
        this.openOverflow = openOverflow;
    }

    public bool Spilled => overflow != null;

    /// <summary>The buffered payload, or null once the stream has spilled to overflow.</summary>
    public byte[]? InlineBytes => overflow == null ? buffered!.ToArray() : null;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position
    {
        get => length;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (overflow == null && length + buffer.Length > threshold)
            await SpillAsync(ct);

        if (overflow != null)
            await overflow.WriteAsync(buffer, ct);
        else
            await buffered!.WriteAsync(buffer, ct);

        length += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();

    async Task SpillAsync(CancellationToken ct)
    {
        overflow = openOverflow();
        buffered!.Position = 0;
        await buffered.CopyToAsync(overflow, ct);
        await buffered.DisposeAsync();
        buffered = null;
    }

    public override void Flush() => overflow?.Flush();

    public override Task FlushAsync(CancellationToken ct) =>
        overflow?.FlushAsync(ct) ?? Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (overflow != null) await overflow.DisposeAsync();
        if (buffered != null) await buffered.DisposeAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            overflow?.Dispose();
            buffered?.Dispose();
        }
        base.Dispose(disposing);
    }
}
