using FoxyOverlay.Services.Utils;
using FoxyOverlay.Services.Utils.Abstractions;

using Microsoft.Extensions.DependencyInjection;


namespace FoxyOverlay.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemTimerFactory(this IServiceCollection services)
    {
        services.AddSingleton<ITimerFactory, SystemTimerFactory>();
        return services;
    }
}