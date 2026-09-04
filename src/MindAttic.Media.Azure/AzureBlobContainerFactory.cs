using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace MindAttic.Media.Azure;

/// <summary>
/// Builds the one <see cref="BlobContainerClient"/> the store and the signer share. Registered as a
/// singleton so the credential handshake and the container existence check happen once per process,
/// not once per scoped request.
/// </summary>
public sealed class AzureBlobContainerFactory
{
    readonly AzureMediaOptions options;
    readonly Lazy<(BlobServiceClient Service, BlobContainerClient Container)> clients;

    public AzureBlobContainerFactory(IOptions<AzureMediaOptions> options)
    {
        this.options = options.Value;
        clients = new Lazy<(BlobServiceClient, BlobContainerClient)>(Build, isThreadSafe: true);
    }

    public BlobContainerClient Container => clients.Value.Container;

    public BlobServiceClient Service => clients.Value.Service;

    public string AccountName => Container.AccountName;

    (BlobServiceClient, BlobContainerClient) Build()
    {
        BlobServiceClient service;

        if (!string.IsNullOrEmpty(options.ConnectionString))
            service = new BlobServiceClient(options.ConnectionString);
        else if (options.BlobServiceUri != null)
            service = new BlobServiceClient(options.BlobServiceUri, new DefaultAzureCredential());
        else
            throw new InvalidOperationException(
                "AzureMediaOptions requires either ConnectionString or BlobServiceUri.");

        var container = service.GetBlobContainerClient(options.ContainerName);
        container.CreateIfNotExists();
        return (service, container);
    }
}
