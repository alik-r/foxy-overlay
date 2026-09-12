using System.Threading.Tasks;


namespace FoxyOverlay.Core.Services.Abstractions;

public interface IConfigService
{
    /// <summary>Absolute path of the backing file, for display in the settings window.</summary>
    string FilePath { get; }

    /// <summary>
    /// Reads config from disk, clamping invalid values. Never throws: a missing or
    /// corrupt file yields defaults, because refusing to start is the worse failure.
    /// </summary>
    Task<Config> LoadAsync();

    Task SaveAsync(Config config);
}
