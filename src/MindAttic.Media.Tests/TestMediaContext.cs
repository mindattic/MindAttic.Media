using Microsoft.EntityFrameworkCore;
using MindAttic.Media;

namespace MindAttic.Media.Tests;

public sealed class TestMediaContext : DbContext
{
    public TestMediaContext(DbContextOptions<TestMediaContext> options) : base(options) { }

    public DbSet<MediaItem> Media => Set<MediaItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaItem>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => m.Uid).IsUnique();
            b.Ignore(m => m.RowVersion);
        });
    }

    public static TestMediaContext Create() =>
        new(new DbContextOptionsBuilder<TestMediaContext>()
            .UseInMemoryDatabase($"media-{Guid.NewGuid():N}")
            .Options);
}

/// <summary>A forward-only source, the shape a browser upload actually hands the store.</summary>
public sealed class NonSeekableStream : Stream
{
    readonly Stream inner;

    public NonSeekableStream(byte[] data) => inner = new MemoryStream(data);

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
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
        inner.ReadAsync(buffer, ct);
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
