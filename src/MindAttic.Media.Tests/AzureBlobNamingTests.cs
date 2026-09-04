using Microsoft.Extensions.Options;
using MindAttic.Media;
using MindAttic.Media.Azure;
using NUnit.Framework;

namespace MindAttic.Media.Tests;

[TestFixture]
public class AzureBlobNamingTests
{
    [Test]
    public void BlobNameIsDerivedFromTheRowAlone()
    {
        var uid = Guid.Parse("11112222-3333-4444-5555-666677778888");
        var item = new MediaItem { Uid = uid, FileName = "clip.mp4" };

        Assert.That(AzureBlobNaming.BlobNameFor(item), Is.EqualTo("11112222333344445555666677778888/clip.mp4"));
        Assert.That(AzureBlobNaming.BlobNameFor(uid, "clip.mp4"), Is.EqualTo(AzureBlobNaming.BlobNameFor(item)));
    }

    [Test]
    public void BlobNameStripsPathTraversal()
    {
        var item = new MediaItem { Uid = Guid.Empty, FileName = "../../secrets.env" };

        Assert.That(AzureBlobNaming.BlobNameFor(item), Is.EqualTo("00000000000000000000000000000000/secrets.env"));
    }

    [Test]
    public void ContainerFactoryFailsClosedWithoutCredentials()
    {
        var factory = new AzureBlobContainerFactory(Options.Create(new AzureMediaOptions()));

        var ex = Assert.Throws<InvalidOperationException>(() => _ = factory.Container);
        Assert.That(ex!.Message, Does.Contain("ConnectionString or BlobServiceUri"));
    }

    [Test]
    public async Task SignerDeclinesInlineAndUnstoredItems()
    {
        var options = Options.Create(new AzureMediaOptions { ConnectionString = "UseDevelopmentStorage=true" });
        var signer = new AzureBlobUrlSigner(options, new AzureBlobContainerFactory(options));

        var inline = new MediaItem { Uid = Guid.NewGuid(), FileName = "a.png", Bytes = [1, 2, 3], BlobUri = "https://x/y" };
        var unstored = new MediaItem { Uid = Guid.NewGuid(), FileName = "a.png" };

        Assert.That(await signer.TryCreateReadUrlAsync(inline, TimeSpan.FromMinutes(5)), Is.Null);
        Assert.That(await signer.TryCreateReadUrlAsync(unstored, TimeSpan.FromMinutes(5)), Is.Null);
    }
}
