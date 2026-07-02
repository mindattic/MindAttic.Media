using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MindAttic.Media;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMedia<TContext>(
        this IServiceCollection services,
        Action<MediaStoreOptions>? configureOptions = null)
        where TContext : DbContext
    {
        if (configureOptions != null)
            services.Configure<MediaStoreOptions>(configureOptions);
        else
            services.Configure<MediaStoreOptions>(_ => { });

        services.AddScoped<IMediaStore, LocalDiskMediaStore<TContext>>();
        return services;
    }
}
