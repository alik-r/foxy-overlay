using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public sealed class MockConfigService : IConfigService
{
    private Config _config;
    private int _loadCount;

    public MockConfigService(Config config) => _config = config;

    public string FilePath => "<in-memory>";
    public int LoadCount => Volatile.Read(ref _loadCount);
    public Config Saved { get; private set; } = new Config();

    /// <summary>Swaps what the next LoadAsync will return, as an external edit would.</summary>
    public void Replace(Config config) => _config = config;

    public Task<Config> LoadAsync()
    {
        Interlocked.Increment(ref _loadCount);
        return Task.FromResult(_config.Clone());
    }

    public Task SaveAsync(Config config)
    {
        Saved = config;
        _config = config;
        return Task.CompletedTask;
    }
}
