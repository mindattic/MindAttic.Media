namespace MindAttic.Media.Azure;

public sealed class AzureMediaOptions : MediaStoreOptions
{
    public Uri? BlobServiceUri { get; set; }
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "media";
}
