namespace MindAttic.Media.Azure;

public sealed class AzureMediaOptions : MediaStoreOptions
{
    /// <summary>e.g. https://myaccount.blob.core.windows.net — authenticated with DefaultAzureCredential.</summary>
    public Uri? BlobServiceUri { get; set; }

    /// <summary>Account connection string. Takes precedence over <see cref="BlobServiceUri"/>; enables key-signed SAS.</summary>
    public string? ConnectionString { get; set; }

    public string ContainerName { get; set; } = "media";

    /// <summary>
    /// True when the container allows anonymous blob reads. The signer then hands out the plain URL
    /// instead of a SAS, which is what lets a CDN in front of the account cache anything at all.
    /// </summary>
    public bool PublicRead { get; set; }

    /// <summary>Optional CDN/custom-domain origin substituted for the blob endpoint when serving.</summary>
    public Uri? PublicBaseUri { get; set; }

    /// <summary>Block size for streamed uploads. Larger blocks cost memory but fewer round trips.</summary>
    public int UploadBlockSizeBytes { get; set; } = 8 * 1024 * 1024;
}
