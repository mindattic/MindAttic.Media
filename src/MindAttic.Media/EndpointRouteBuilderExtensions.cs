using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MindAttic.Media;

public static class EndpointRouteBuilderExtensions
{
    static readonly string[] InlinePrefixes = ["image/", "text/", "video/", "audio/"];

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_media/{uid:guid}", async (Guid uid, HttpContext http) =>
        {
            // Every response from this endpoint, redirects and 404s included.
            http.Response.Headers.XContentTypeOptions = "nosniff";

            var store = http.RequestServices.GetRequiredService<IMediaStore>();
            var options = http.RequestServices.GetRequiredService<IOptions<MediaStoreOptions>>().Value;

            var meta = await store.GetMetaAsync(uid, http.RequestAborted);
            if (meta == null)
                return Results.NotFound();

            // Hand the browser straight to the storage service when the backend can mint a URL. This
            // is the only path that gives large media working Range requests and seeking, and it keeps
            // the bytes out of the app entirely.
            var signer = http.RequestServices.GetService<IMediaUrlSigner>();
            if (signer != null)
            {
                var signed = await signer.TryCreateReadUrlAsync(meta, options.SignedUrlLifetime, http.RequestAborted);
                if (signed != null)
                    return Results.Redirect(signed.AbsoluteUri);
            }

            // Pre-signer rows (and any backend that stores a plain public URL) still redirect as before.
            if (Uri.TryCreate(meta.BlobUri, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
            {
                return Results.Redirect(absolute.AbsoluteUri);
            }

            var result = await store.GetAsync(uid, http.RequestAborted);
            if (result == null)
                return Results.NotFound();

            var (_, stream) = result.Value;

            // The content type is whatever the uploader claimed, served from the app's own origin, so
            // anything a browser would execute (HTML, SVG, XML, script) must never render here: it is
            // forced to download and sandboxed in case a client ignores the disposition, and nosniff
            // stops a browser from second-guessing a harmless type into an active one.
            var mediaType = MediaContentTypes.Normalize(meta.ContentType);
            var active = mediaType == null || MediaContentTypes.IsActive(mediaType);

            var inline = !active
                && (InlinePrefixes.Any(p => mediaType!.StartsWith(p, StringComparison.Ordinal))
                    || mediaType == "application/pdf");

            if (active)
                http.Response.Headers.ContentSecurityPolicy = MediaContentTypes.SandboxPolicy;

            if (options.CacheMaxAgeSeconds > 0)
                http.Response.Headers.CacheControl = $"private, max-age={options.CacheMaxAgeSeconds}";

            if (inline)
                http.Response.Headers.ContentDisposition = "inline";

            var etag = string.IsNullOrEmpty(meta.Sha256) ? null : new EntityTagHeaderValue($"\"{meta.Sha256}\"");

            return Results.Stream(
                stream,
                contentType: mediaType == null ? "application/octet-stream" : meta.ContentType,
                fileDownloadName: inline ? null : meta.FileName,
                lastModified: DateTime.SpecifyKind(meta.ModifiedUtc, DateTimeKind.Utc),
                entityTag: etag,
                enableRangeProcessing: true);
        });

        return app;
    }
}
