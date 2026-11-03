namespace MindAttic.Media.Azure;

public static class AzureBlobNaming
{
    /// <summary>
    /// The blob path for a NEW upload: <c>{uid:N}/{filename}</c>, with the portable sanitizer. The
    /// store stores the same sanitized name on the row, so <see cref="BlobNameFor(MediaItem)"/>
    /// reconstructs this path from the row alone.
    /// </summary>
    public static string BlobNameFor(Guid uid, string fileName) =>
        $"{uid:N}/{MediaNaming.SanitizeFileName(fileName)}";

    /// <summary>
    /// The blob path of an existing row. Derived, never stored — the uid is the only identity the
    /// CMS ever quotes. Uses the legacy (pre-portable) sanitizer, so a row uploaded on Linux with a
    /// backslash or colon still kept in its name resolves to the blob it was written to; for rows
    /// named by the portable sanitizer the two agree.
    /// </summary>
    public static string BlobNameFor(MediaItem item) =>
        $"{item.Uid:N}/{MediaNaming.LegacySanitizeFileName(item.FileName)}";
}
