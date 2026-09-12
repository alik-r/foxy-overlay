using Microsoft.Extensions.DependencyInjection;

using FoxyOverlay.Core.Packs;
using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers config, logging, stats and pack loading as singletons.</summary>
    public static IServiceCollection AddFoxyCore(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IStatsService, StatsService>();
        services.AddSingleton<PackLoader>();
        services.AddSingleton<PackBaker>();
        return services;
    }
}
