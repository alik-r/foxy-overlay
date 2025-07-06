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

    public ConfigService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }
    
    public ConfigService()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FoxyOverlay",
                "config.json"))
    {
    }
    
    public async Task<Config> LoadAsync()
    {
        // TODO: Add logging
        try
        {
            if (!File.Exists(_filePath))
                return new Config();

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            var cfg = JsonSerializer.Deserialize<Config>(json, _jsonOptions);
            return cfg ?? new Config();
        }
        catch (JsonException)
        {
            return new Config();
        }
    }

    public async Task SaveAsync(Config config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}