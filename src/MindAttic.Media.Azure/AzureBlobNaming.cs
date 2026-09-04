namespace MindAttic.Media.Azure;

public static class AzureBlobNaming
{
    /// <summary>
    /// The blob path for an item: <c>{uid:N}/{filename}</c>. Derived, never stored — the uid is the
    /// only identity the CMS ever quotes, so the layout must be reconstructable from the row alone.
    /// </summary>
    public static string BlobNameFor(Guid uid, string fileName) =>
        $"{uid:N}/{MediaNaming.SanitizeFileName(fileName)}";

    public static string BlobNameFor(MediaItem item) => BlobNameFor(item.Uid, item.FileName);
}
