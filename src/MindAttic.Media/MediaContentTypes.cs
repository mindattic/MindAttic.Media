using Microsoft.Net.Http.Headers;

namespace MindAttic.Media;

/// <summary>
/// Classifies an uploader-supplied content type. "Active" types are ones a browser can run script
/// from (HTML, SVG, XML, JavaScript and the like); they must never render inline on the serving
/// origin, or any uploader gets stored XSS against everyone who opens the link.
/// </summary>
public static class MediaContentTypes
{
    /// <summary>CSP sent with active content: no script, no same-origin access, no subresources.</summary>
    public const string SandboxPolicy = "sandbox; default-src 'none'";

    static readonly HashSet<string> ActiveMediaTypes = new(StringComparer.Ordinal)
    {
        "text/html", "application/xhtml+xml", "image/svg+xml", "text/xml", "application/xml",
        "text/xsl", "application/xslt+xml", "text/javascript", "application/javascript",
        "application/x-javascript", "text/ecmascript", "application/ecmascript", "text/x-javascript",
        "application/x-shockwave-flash", "text/x-component", "multipart/x-mixed-replace"
    };

    /// <summary>The bare, lower-cased media type ("text/html"), or null when it does not parse.</summary>
    public static string? Normalize(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed) && parsed.MediaType.HasValue
            ? parsed.MediaType.Value!.Trim().ToLowerInvariant()
            : null;

    /// <summary>
    /// True for the known active types, any XML-based (+xml) type, and anything that does not parse
    /// (fail safe: an unrecognizable type is treated as dangerous).
    /// </summary>
    public static bool IsActive(string? contentType)
    {
        var mediaType = Normalize(contentType);
        return mediaType == null
            || ActiveMediaTypes.Contains(mediaType)
            || mediaType.EndsWith("+xml", StringComparison.Ordinal);
    }
}
