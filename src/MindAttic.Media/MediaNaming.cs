namespace MindAttic.Media;

public static class MediaNaming
{
    // Windows' invalid file-name set plus '/', fixed so a name sanitizes the same on every OS.
    static readonly char[] PortableInvalidChars =
    [
        '"', '<', '>', '|', ':', '*', '?', '\\', '/',
        .. Enumerable.Range(0, 32).Select(i => (char)i)
    ];

    /// <summary>
    /// The stored file name for a NEW upload. Platform-independent: a browser on Windows can send
    /// <c>C:\videos\clip.mp4</c>, and a Linux host must strip that path the same way Windows does,
    /// instead of keeping the backslashes and colon in the stored name and blob path.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        var name = fileName ?? "";
        var cut = name.LastIndexOfAny(['/', '\\']);
        if (cut >= 0) name = name[(cut + 1)..];
        foreach (var c in PortableInvalidChars)
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? "file" : name;
    }

    /// <summary>
    /// The pre-2026-11 sanitizer, which depended on the host OS. Kept only so the blob path of an
    /// existing row (derived from its stored file name) resolves exactly as it did when uploaded.
    /// Idempotent on any output of <see cref="SanitizeFileName"/>, so new rows resolve the same.
    /// Never use it to name a new upload.
    /// </summary>
    public static string LegacySanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? "file" : name;
    }
}
