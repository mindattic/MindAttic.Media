using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace MindAttic.Media.Azure;

/// <summary>
/// Mints the URL that <c>/_media/{uid}</c> redirects to. Three modes, in order of preference:
/// a plain (or CDN-fronted) URL when the container is public; a key-signed SAS when the account was
/// configured with a connection string; a user-delegation SAS when it was configured with Entra
/// credentials. Anything else declines, and the endpoint streams through the app instead.
/// </summary>
public sealed class AzureBlobUrlSigner : IMediaUrlSigner
{
    static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);
    static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromHours(6);

    readonly AzureMediaOptions options;
    readonly AzureBlobContainerFactory containers;
    readonly SemaphoreSlim delegationLock = new(1, 1);
    UserDelegationKey? delegationKey;

    public AzureBlobUrlSigner(IOptions<AzureMediaOptions> options, AzureBlobContainerFactory containers)
    {
        this.options = options.Value;
        this.containers = containers;
    }

    public async ValueTask<Uri?> TryCreateReadUrlAsync(MediaItem item, TimeSpan lifetime, CancellationToken ct = default)
    {
        // An inline payload never reached blob storage; the endpoint serves it from the row.
        if (item.Bytes != null || item.BlobUri == null)
            return null;

        var blob = containers.Container.GetBlobClient(AzureBlobNaming.BlobNameFor(item));

        if (options.PublicRead)
            return Rebase(blob.Uri);

        var expiresOn = DateTimeOffset.UtcNow.Add(lifetime);

        if (blob.CanGenerateSasUri)
            return blob.GenerateSasUri(BlobSasPermissions.Read, expiresOn);

        var key = await GetDelegationKeyAsync(ct);
        if (key == null)
            return null;

        var builder = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
        {
            BlobContainerName = containers.Container.Name,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.Subtract(ClockSkew),
            ContentType = item.ContentType
        };

        var uri = new UriBuilder(blob.Uri)
        {
            Query = builder.ToSasQueryParameters(key, containers.AccountName).ToString()
        };
        return uri.Uri;
    }

    Uri Rebase(Uri blobUri) =>
        options.PublicBaseUri == null
            ? blobUri
            : new Uri(options.PublicBaseUri, blobUri.AbsolutePath);

    async ValueTask<UserDelegationKey?> GetDelegationKeyAsync(CancellationToken ct)
    {
        var current = delegationKey;
        if (current != null && current.SignedExpiresOn > DateTimeOffset.UtcNow.Add(ClockSkew))
            return current;

        await delegationLock.WaitAsync(ct);
        try
        {
            current = delegationKey;
            if (current != null && current.SignedExpiresOn > DateTimeOffset.UtcNow.Add(ClockSkew))
                return current;

            var response = await containers.Service.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow.Subtract(ClockSkew),
                DateTimeOffset.UtcNow.Add(DelegationKeyLifetime),
                ct);

            delegationKey = response.Value;
            return delegationKey;
        }
        finally
        {
            delegationLock.Release();
        }
    }
}
