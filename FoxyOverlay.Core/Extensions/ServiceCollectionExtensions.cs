using Microsoft.Extensions.DependencyInjection;

using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfigService(this IServiceCollection services)
    {
        services.AddSingleton<IConfigService, ConfigService>();
        return services;
    }
}