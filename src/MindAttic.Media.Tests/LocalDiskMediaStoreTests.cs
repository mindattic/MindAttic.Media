using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MindAttic.Media;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class LocalDiskMediaStoreTests
{
    string root = "";
    TestMediaContext context = null!;
    LocalDiskMediaStore<TestMediaContext> store = null!;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "ma-media-tests", Guid.NewGuid().ToString("N"));
        context = TestMediaContext.Create();
        store = new LocalDiskMediaStore<TestMediaContext>(
            context,
            Options.Create(new MediaStoreOptions { MediaRoot = root, InlineThresholdBytes = 1024 }));
    }

    [TearDown]
    public void TearDown()
    {
        context.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [Test]
    public async Task Upload_UnderThreshold_StoresInlineWithCorrectHash()
    {
        var payload = new byte[512];
        Random.Shared.NextBytes(payload);

        var item = await store.UploadAsync(new NonSeekableStream(payload), "small.png", "image/png");

        Assert.Multiple(() =>
        {
            Assert.That(item.Bytes, Is.EqualTo(payload));
            Assert.That(item.BlobUri, Is.Null);
            Assert.That(item.SizeBytes, Is.EqualTo(payload.Length));
            Assert.That(item.Sha256, Is.EqualTo(Sha256Hex(payload)));
            Assert.That(Directory.Exists(root), Is.False, "nothing should reach disk under the threshold");
        });
    }

    [Test]
    public async Task Upload_AtExactlyThreshold_StaysInline()
    {
        var payload = new byte[1024];
        Random.Shared.NextBytes(payload);

        var item = await store.UploadAsync(new NonSeekableStream(payload), "edge.bin", "application/octet-stream");

        Assert.That(item.Bytes, Is.Not.Null);
        Assert.That(item.BlobUri, Is.Null);
    }

    [Test]
    public async Task Upload_OverThreshold_SpillsToDiskWithIntactBytesAndHash()
    {
        var payload = new byte[300_000];
        Random.Shared.NextBytes(payload);

        var item = await store.UploadAsync(new NonSeekableStream(payload), "clip.mp4", "video/mp4");

        Assert.Multiple(() =>
        {
            Assert.That(item.Bytes, Is.Null, "a large payload must not be inlined into the row");
            Assert.That(item.BlobUri, Is.Not.Null);
            Assert.That(item.SizeBytes, Is.EqualTo(payload.Length));
            Assert.That(item.Sha256, Is.EqualTo(Sha256Hex(payload)));
        });
        Assert.That(await File.ReadAllBytesAsync(item.BlobUri!), Is.EqualTo(payload));
    }

    [Test]
    public async Task Get_ReturnsSeekableStreamForSpilledPayload()
    {
        var payload = new byte[300_000];
        Random.Shared.NextBytes(payload);
        var item = await store.UploadAsync(new NonSeekableStream(payload), "clip.mp4", "video/mp4");

        var result = await store.GetAsync(item.Uid);

        Assert.That(result, Is.Not.Null);
        await using var content = result!.Value.Content;
        Assert.That(content.CanSeek, Is.True, "range requests need a seekable stream");
        Assert.That(content.Length, Is.EqualTo(payload.Length));
    }

    [Test]
    public async Task GetMeta_ReturnsRowWithoutTouchingPayload()
    {
        var payload = new byte[300_000];
        var item = await store.UploadAsync(new NonSeekableStream(payload), "clip.mp4", "video/mp4");
        File.Delete(item.BlobUri!);

        var meta = await store.GetMetaAsync(item.Uid);

        Assert.That(meta, Is.Not.Null);
        Assert.That(meta!.FileName, Is.EqualTo("clip.mp4"));
    }

    [Test]
    public async Task Delete_IsSoftAndHidesTheItem()
    {
        var item = await store.UploadAsync(new NonSeekableStream([1, 2, 3]), "a.txt", "text/plain");

        Assert.That(await store.DeleteAsync(item.Uid), Is.True);
        Assert.That(await store.GetMetaAsync(item.Uid), Is.Null);
        Assert.That(await store.ListAsync(), Is.Empty);
        Assert.That(context.Media.Single().IsDeleted, Is.True);
    }

    [Test]
    public async Task Upload_SanitisesTraversalInFileName()
    {
        var item = await store.UploadAsync(new NonSeekableStream([1]), "../../evil.txt", "text/plain");

        Assert.That(item.FileName, Is.EqualTo("evil.txt"));
    }
}
