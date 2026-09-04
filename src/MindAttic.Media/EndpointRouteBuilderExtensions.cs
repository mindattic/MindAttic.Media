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
                    return Results.Redirect(signed.ToString());
            }

            // Pre-signer rows (and any backend that stores a plain public URL) still redirect as before.
            if (Uri.TryCreate(meta.BlobUri, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
            {
                return Results.Redirect(absolute.ToString());
            }

            var result = await store.GetAsync(uid, http.RequestAborted);
            if (result == null)
                return Results.NotFound();

            var (_, stream) = result.Value;

            var inline = InlinePrefixes.Any(p => meta.ContentType.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                || meta.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

            if (options.CacheMaxAgeSeconds > 0)
                http.Response.Headers.CacheControl = $"private, max-age={options.CacheMaxAgeSeconds}";

            if (inline)
                http.Response.Headers.ContentDisposition = "inline";

            var etag = string.IsNullOrEmpty(meta.Sha256) ? null : new EntityTagHeaderValue($"\"{meta.Sha256}\"");

            return Results.Stream(
                stream,
                contentType: meta.ContentType,
                fileDownloadName: inline ? null : meta.FileName,
                lastModified: DateTime.SpecifyKind(meta.ModifiedUtc, DateTimeKind.Utc),
                entityTag: etag,
                enableRangeProcessing: true);
        });

        return app;
    }
}
