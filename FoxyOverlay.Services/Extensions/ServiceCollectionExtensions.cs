using Microsoft.Extensions.DependencyInjection;

using FoxyOverlay.Services.Utils;
using FoxyOverlay.Services.Utils.Abstractions;


namespace FoxyOverlay.Services.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the jumpscare loop. The host must also register an
    /// <see cref="Abstractions.IJumpscarePlayer"/>; only the UI layer can provide one.
    /// </summary>
    public static IServiceCollection AddFoxyServices(this IServiceCollection services)
    {
        services.AddSingleton<ITimerFactory, SystemTimerFactory>();
        services.AddSingleton<JumpscareService>();
        return services;
    }
}
