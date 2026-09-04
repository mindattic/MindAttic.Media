using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindAttic.Media;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class MediaEndpointTests
{
    string root = "";
    WebApplication app = null!;
    HttpClient client = null!;

    /// <summary>A signer that always answers, standing in for the Azure SAS minting we cannot reach in a unit test.</summary>
    sealed class StubSigner : IMediaUrlSigner
    {
        public Uri? Answer { get; set; }
        public TimeSpan? SawLifetime { get; private set; }

        public ValueTask<Uri?> TryCreateReadUrlAsync(MediaItem item, TimeSpan lifetime, CancellationToken ct = default)
        {
            SawLifetime = lifetime;
            return ValueTask.FromResult(Answer);
        }
    }

    StubSigner? signer;

    async Task<HttpClient> StartAsync(bool withSigner = false)
    {
        root = Path.Combine(Path.GetTempPath(), "ma-media-endpoint", Guid.NewGuid().ToString("N"));
        var dbName = $"endpoint-{Guid.NewGuid():N}";

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<TestMediaContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddMedia<TestMediaContext>(o =>
        {
            o.MediaRoot = root;
            o.InlineThresholdBytes = 1024;
            o.SignedUrlLifetime = TimeSpan.FromMinutes(17);
        });

        if (withSigner)
        {
            signer = new StubSigner();
            builder.Services.AddSingleton<IMediaUrlSigner>(signer);
        }

        app = builder.Build();
        app.MapMediaEndpoints();
        await app.StartAsync();
        client = app.GetTestClient();
        return client;
    }

    async Task<MediaItem> SeedAsync(byte[] payload, string fileName, string contentType)
    {
        using var scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        return await store.UploadAsync(new NonSeekableStream(payload), fileName, contentType);
    }

    [TearDown]
    public async Task TearDown()
    {
        client?.Dispose();
        if (app != null) await app.DisposeAsync();
        signer = null;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task ServesInlinePayloadWithEtagAndRangeSupport()
    {
        await StartAsync();
        var payload = new byte[512];
        Random.Shared.NextBytes(payload);
        var item = await SeedAsync(payload, "small.png", "image/png");

        var response = await client.GetAsync($"/_media/{item.Uid}");
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/png"));
            Assert.That(response.Headers.ETag!.Tag, Is.EqualTo($"\"{item.Sha256}\""));
            Assert.That(response.Headers.AcceptRanges, Does.Contain("bytes"));
            Assert.That(body, Is.EqualTo(payload));
        });
    }

    [Test]
    public async Task ServesAByteRangeOutOfALargeSpilledPayload()
    {
        await StartAsync();
        var payload = new byte[300_000];
        Random.Shared.NextBytes(payload);
        var item = await SeedAsync(payload, "clip.mp4", "video/mp4");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/_media/{item.Uid}");
        request.Headers.Range = new RangeHeaderValue(1000, 1999);
        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent),
            "video seeking depends on the endpoint honouring Range");
        Assert.That(response.Content.Headers.ContentRange!.From, Is.EqualTo(1000));
        Assert.That(response.Content.Headers.ContentRange.To, Is.EqualTo(1999));
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(payload[1000..2000]));
    }

    [Test]
    public async Task RepeatRequestWithMatchingEtagIsNotModified()
    {
        await StartAsync();
        var item = await SeedAsync([1, 2, 3, 4], "a.png", "image/png");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/_media/{item.Uid}");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{item.Sha256}\""));
        var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
    }

    [Test]
    public async Task NonInlineTypeIsServedAsAnAttachment()
    {
        await StartAsync();
        var item = await SeedAsync([1, 2, 3, 4], "bundle.zip", "application/zip");

        var response = await client.GetAsync($"/_media/{item.Uid}");

        Assert.That(response.Content.Headers.ContentDisposition!.DispositionType, Is.EqualTo("attachment"));
        Assert.That(response.Content.Headers.ContentDisposition.FileName, Does.Contain("bundle.zip"));
    }

    [Test]
    public async Task UnknownUidIs404()
    {
        await StartAsync();

        var response = await client.GetAsync($"/_media/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeletedItemIs404()
    {
        await StartAsync();
        var item = await SeedAsync([1, 2, 3, 4], "a.png", "image/png");
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IMediaStore>().DeleteAsync(item.Uid);

        var response = await client.GetAsync($"/_media/{item.Uid}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RedirectsToASignedUrlWhenASignerIsRegistered()
    {
        var noRedirect = await StartAsync(withSigner: true);
        signer!.Answer = new Uri("https://cdn.example.com/media/clip.mp4?sig=abc");
        var item = await SeedAsync(new byte[300_000], "clip.mp4", "video/mp4");

        var response = await noRedirect.GetAsync($"/_media/{item.Uid}");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Found));
            Assert.That(response.Headers.Location!.ToString(), Is.EqualTo("https://cdn.example.com/media/clip.mp4?sig=abc"));
            Assert.That(signer.SawLifetime, Is.EqualTo(TimeSpan.FromMinutes(17)),
                "the endpoint must pass the configured lifetime through to the signer");
        });
    }

    [Test]
    public async Task FallsBackToStreamingWhenTheSignerDeclines()
    {
        await StartAsync(withSigner: true);
        signer!.Answer = null;
        var payload = new byte[512];
        Random.Shared.NextBytes(payload);
        var item = await SeedAsync(payload, "small.png", "image/png");

        var response = await client.GetAsync($"/_media/{item.Uid}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(payload));
    }
}
