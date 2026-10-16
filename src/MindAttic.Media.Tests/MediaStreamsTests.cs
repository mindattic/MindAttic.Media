using System.Security.Cryptography;
using MindAttic.Media;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class MediaStreamsTests
{
    const int SmallBufferSize = 16;

    static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [TestCase(0)]  // empty
    [TestCase(1)]  // single-byte
    [TestCase(SmallBufferSize)]  // exactly buffer-size
    [TestCase(50)]  // multi-buffer
    public async Task HashAndLengthMatchSha256OfSource(int payloadLength)
    {
        var payload = new byte[payloadLength];
        Random.Shared.NextBytes(payload);
        var destination = new MemoryStream();

        var (sha, length) = await MediaStreams.CopyAndHashAsync(
            new MemoryStream(payload), destination, SmallBufferSize);

        Assert.Multiple(() =>
        {
            Assert.That(length, Is.EqualTo(payloadLength));
            Assert.That(sha, Is.EqualTo(Sha256Hex(payload)));
            Assert.That(destination.ToArray(), Is.EqualTo(payload));
        });
    }

    [Test]
    public async Task HashIsLowercaseHex()
    {
        var payload = new byte[100];
        Random.Shared.NextBytes(payload);

        var (sha, _) = await MediaStreams.CopyAndHashAsync(new MemoryStream(payload), new MemoryStream());

        Assert.That(sha, Does.Match("^[0-9a-f]{64}$"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public async Task BufferSizeZeroOrNegativeFallsBackToDefault(int bufferSize)
    {
        var source = new BufferSizeCapturingStream(new byte[10]);

        await MediaStreams.CopyAndHashAsync(source, new MemoryStream(), bufferSize);

        Assert.That(source.LastRequestedSize, Is.EqualTo(MediaStreams.DefaultCopyBufferSize));
    }

    [Test]
    public void CancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var source = new ThrowsOnCancelStream(new byte[10]);

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await MediaStreams.CopyAndHashAsync(source, new MemoryStream(), ct: cts.Token));
    }

    [Test]
    public async Task NonSeekableSourceIsReadOnce()
    {
        var payload = new byte[50_000];
        Random.Shared.NextBytes(payload);
        var destination = new MemoryStream();

        var (sha, length) = await MediaStreams.CopyAndHashAsync(
            new NonSeekableStream(payload), destination, bufferSize: 4096);

        Assert.Multiple(() =>
        {
            Assert.That(length, Is.EqualTo(payload.Length));
            Assert.That(sha, Is.EqualTo(Sha256Hex(payload)));
            Assert.That(destination.ToArray(), Is.EqualTo(payload));
        });
    }

    /// <summary>Records the size of the buffer slice each ReadAsync call actually receives.</summary>
    sealed class BufferSizeCapturingStream : Stream
    {
        readonly MemoryStream inner;

        public BufferSizeCapturingStream(byte[] data) => inner = new MemoryStream(data);

        public int LastRequestedSize { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            LastRequestedSize = buffer.Length;
            return inner.ReadAsync(buffer, ct);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A source that honours cancellation on every ReadAsync call, used to prove the token is forwarded.</summary>
    sealed class ThrowsOnCancelStream : Stream
    {
        readonly MemoryStream inner;

        public ThrowsOnCancelStream(byte[] data) => inner = new MemoryStream(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return inner.ReadAsync(buffer, ct);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
