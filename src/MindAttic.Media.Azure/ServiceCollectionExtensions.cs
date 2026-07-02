using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindAttic.Media;

namespace MindAttic.Media.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediaAzure<TContext>(
        this IServiceCollection services,
        Action<AzureMediaOptions>? configureOptions = null)
        where TContext : DbContext
    {
        if (configureOptions != null)
            services.Configure<AzureMediaOptions>(configureOptions);
        else
            services.Configure<AzureMediaOptions>(_ => { });

        services.AddScoped<IMediaStore, AzureBlobMediaStore<TContext>>();
        return services;
    }
}
