using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MindAttic.Media;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_media/{uid:guid}", async (Guid uid, HttpContext http) =>
        {
            var store = http.RequestServices.GetRequiredService<IMediaStore>();
            var result = await store.GetAsync(uid, http.RequestAborted);

            if (result == null)
                return Results.NotFound();

            var (meta, stream) = result.Value;

            if (meta.BlobUri != null && meta.BlobUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                stream.Dispose();
                return Results.Redirect(meta.BlobUri);
            }

            var inline = meta.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || meta.ContentType == "application/pdf"
                || meta.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

            var disposition = inline ? "inline" : $"attachment; filename=\"{meta.FileName}\"";
            http.Response.Headers.ContentDisposition = disposition;

            return Results.Stream(stream, meta.ContentType);
        });

        return app;
    }
}
