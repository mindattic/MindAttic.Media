using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MindAttic.Media;

namespace MindAttic.Media.Azure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces any previously registered <see cref="IMediaStore"/> with the Azure Blob backend and
    /// registers the URL signer that turns <c>/_media/{uid}</c> into a redirect.
    /// </summary>
    public static IServiceCollection AddMediaAzure<TContext>(
        this IServiceCollection services,
        Action<AzureMediaOptions>? configureOptions = null)
        where TContext : DbContext
    {
        if (configureOptions != null)
        {
            services.Configure<AzureMediaOptions>(configureOptions);
            // MediaStoreOptions is what the endpoint reads for lifetime/caching, and it is a separate
            // options instance from the derived AzureMediaOptions — keep the shared knobs in step.
            services.Configure<MediaStoreOptions>(o =>
            {
                var azure = new AzureMediaOptions();
                configureOptions(azure);
                o.MediaRoot = azure.MediaRoot;
                o.InlineThresholdBytes = azure.InlineThresholdBytes;
                o.SignedUrlLifetime = azure.SignedUrlLifetime;
                o.CacheMaxAgeSeconds = azure.CacheMaxAgeSeconds;
                o.CopyBufferSize = azure.CopyBufferSize;
            });
        }
        else
        {
            services.Configure<AzureMediaOptions>(_ => { });
        }

        services.TryAddSingleton<AzureBlobContainerFactory>();
        services.RemoveAll<IMediaStore>();
        services.AddScoped<IMediaStore, AzureBlobMediaStore<TContext>>();
        services.RemoveAll<IMediaUrlSigner>();
        services.AddSingleton<IMediaUrlSigner, AzureBlobUrlSigner>();
        return services;
    }
}
