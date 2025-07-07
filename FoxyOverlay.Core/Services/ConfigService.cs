using System.Text.Json;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Services;

public class ConfigService : IConfigService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    private readonly ILoggingService _logger;

    public ConfigService(ILoggingService logger, string filePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }
    
    public ConfigService(ILoggingService logger)
        : this(logger,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FoxyOverlay",
                "config.json"))
    {
    }
    
    public async Task<Config> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new Config();
        
        try
        {
            string json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            Config? cfg = JsonSerializer.Deserialize<Config>(json, _jsonOptions);
            await _logger.LogInfoAsync($"config loaded from {_filePath}");
            return cfg ?? new Config();
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"failed to load config from {_filePath}: {ex.Message}");
            return new Config();
        }
    }

    public async Task SaveAsync(Config config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
            await _logger.LogInfoAsync($"config saved to {_filePath}");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"failed to save config to {_filePath}: {ex.Message}");
            throw;
        }
    }
}