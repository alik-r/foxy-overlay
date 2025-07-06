namespace FoxyOverlay.Core.Services.Abstractions;

public interface IConfigService
{
    Task<Config> LoadAsync();
    
    Task SaveAsync(Config config);
}