using System.Threading.Tasks;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public class MockConfigService : IConfigService
{
    private readonly Config _config;
    public MockConfigService(Config config) => _config = config;
    public Task<Config> LoadAsync() => Task.FromResult(_config);
    public Task SaveAsync(Config config) => Task.CompletedTask;
}