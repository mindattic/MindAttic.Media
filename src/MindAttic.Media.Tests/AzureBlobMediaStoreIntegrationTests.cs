using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MindAttic.Media;
using MindAttic.Media.Azure;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

/// <summary>
/// End-to-end against the Azurite blob emulator. Skipped when Azurite is not listening, so the suite
/// stays green on a machine without it:
/// <code>npx azurite --silent --location ./azurite --blobHost 127.0.0.1 --blobPort 10000</code>
/// This is the only test that proves the thing the Azure backend exists for: a large payload streams
/// up without being buffered, and comes back down through a signed URL that honours Range requests.
/// </summary>
[TestFixture]
[Category("Azurite")]
public class AzureBlobMediaStoreIntegrationTests
{
    const string EmulatorConnectionString = "UseDevelopmentStorage=true";
    const string EmulatorHost = "127.0.0.1";
    const int EmulatorPort = 10000;

    TestMediaContext context = null!;
    AzureBlobContainerFactory factory = null!;
    AzureBlobMediaStore<TestMediaContext> store = null!;
    AzureMediaOptions options = null!;

    static bool EmulatorIsListening()
    {
        try
        {
            using var probe = new TcpClient();
            return probe.ConnectAsync(EmulatorHost, EmulatorPort).Wait(TimeSpan.FromSeconds(2))
                && probe.Connected;
        }
        catch
        {
            return false;
        }
    }

    [SetUp]
    public void SetUp()
    {
        if (!EmulatorIsListening())
            Assert.Ignore($"Azurite is not listening on {EmulatorHost}:{EmulatorPort}.");

        options = new AzureMediaOptions
        {
            ConnectionString = EmulatorConnectionString,
            ContainerName = $"media-{Guid.NewGuid():N}",
            UploadBlockSizeBytes = 1024 * 1024
        };
        context = TestMediaContext.Create();
        factory = new AzureBlobContainerFactory(Options.Create(options));
        store = new AzureBlobMediaStore<TestMediaContext>(context, Options.Create(options), factory);
    }

    [TearDown]
    public void TearDown()
    {
        try { factory?.Container.DeleteIfExists(); } catch { /* emulator teardown is best-effort */ }
        context?.Dispose();
    }

    static string Sha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [Test]
    public async Task LargePayloadStreamsUpAndBackWithItsHashIntact()
    {
        var payload = new byte[12 * 1024 * 1024];
        Random.Shared.NextBytes(payload);

        var item = await store.UploadAsync(new NonSeekableStream(payload), "feature.mp4", "video/mp4");

        Assert.Multiple(() =>
        {
            Assert.That(item.Bytes, Is.Null, "blob-backed media must never be inlined into the row");
            Assert.That(item.SizeBytes, Is.EqualTo(payload.Length));
            Assert.That(item.Sha256, Is.EqualTo(Sha256Hex(payload)));
            Assert.That(item.BlobUri, Does.Contain(AzureBlobNaming.BlobNameFor(item)));
        });

        var round = await store.GetAsync(item.Uid);
        Assert.That(round, Is.Not.Null);
        using var downloaded = new MemoryStream();
        await round!.Value.Content.CopyToAsync(downloaded);
        Assert.That(Sha256Hex(downloaded.ToArray()), Is.EqualTo(item.Sha256));
    }

    [Test]
    public async Task UploadSetsTheBlobContentTypeSoARedirectPlaysInsteadOfDownloading()
    {
        var item = await store.UploadAsync(new NonSeekableStream(new byte[2048]), "clip.mp4", "video/mp4");

        var blob = factory.Container.GetBlobClient(AzureBlobNaming.BlobNameFor(item));
        var properties = await blob.GetPropertiesAsync();

        Assert.That(properties.Value.ContentType, Is.EqualTo("video/mp4"));
    }

    [Test]
    public async Task SignedUrlServesTheBytesAndHonoursRangeRequests()
    {
        var payload = new byte[3 * 1024 * 1024];
        Random.Shared.NextBytes(payload);
        var item = await store.UploadAsync(new NonSeekableStream(payload), "clip.mp4", "video/mp4");

        var signer = new AzureBlobUrlSigner(Options.Create(options), factory);
        var url = await signer.TryCreateReadUrlAsync(item, TimeSpan.FromMinutes(10));

        Assert.That(url, Is.Not.Null, "a connection-string account can key-sign a SAS");

        using var http = new HttpClient();

        var whole = await http.GetAsync(url);
        Assert.That(whole.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Sha256Hex(await whole.Content.ReadAsByteArrayAsync()), Is.EqualTo(item.Sha256));

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(1_000_000, 1_000_999);
        var partial = await http.SendAsync(request);

        Assert.Multiple(async () =>
        {
            Assert.That(partial.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent),
                "seeking a video depends on the storage service serving ranges directly");
            Assert.That(await partial.Content.ReadAsByteArrayAsync(), Is.EqualTo(payload[1_000_000..1_001_000]));
        });
    }

    [Test]
    public async Task SignedUrlExpires()
    {
        var item = await store.UploadAsync(new NonSeekableStream([1, 2, 3, 4]), "a.bin", "application/octet-stream");
        var signer = new AzureBlobUrlSigner(Options.Create(options), factory);

        var url = await signer.TryCreateReadUrlAsync(item, TimeSpan.FromSeconds(-30));

        using var http = new HttpClient();
        var response = await http.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
            "an expired SAS must stop working — that is the point of not exposing the container");
    }

    [Test]
    public async Task PublicReadModeHandsOutThePlainUrlRebasedOnTheCdnOrigin()
    {
        var item = await store.UploadAsync(new NonSeekableStream([1, 2, 3, 4]), "a.bin", "application/octet-stream");
        options.PublicRead = true;
        options.PublicBaseUri = new Uri("https://cdn.mindattic.com");
        var signer = new AzureBlobUrlSigner(Options.Create(options), factory);

        var url = await signer.TryCreateReadUrlAsync(item, TimeSpan.FromMinutes(10));

        Assert.Multiple(() =>
        {
            Assert.That(url!.Host, Is.EqualTo("cdn.mindattic.com"));
            Assert.That(url.AbsolutePath, Does.EndWith(AzureBlobNaming.BlobNameFor(item)));
            Assert.That(url.Query, Is.Empty, "a public container needs no SAS, and a query kills CDN caching");
        });
    }

    [Test]
    public async Task DeleteIsSoftAndLeavesTheBlobRecoverable()
    {
        var item = await store.UploadAsync(new NonSeekableStream([1, 2, 3, 4]), "a.bin", "application/octet-stream");

        Assert.That(await store.DeleteAsync(item.Uid), Is.True);
        Assert.That(await store.GetMetaAsync(item.Uid), Is.Null);

        var blob = factory.Container.GetBlobClient(AzureBlobNaming.BlobNameFor(item));
        Assert.That((await blob.ExistsAsync()).Value, Is.True, "HOUSE-LAW-2: disable, do not destroy");
    }
}
