using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Services;

public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILoggingService _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public ConfigService(ILoggingService logger)
        : this(logger, AppPaths.ConfigFile)
    {
    }

    public ConfigService(ILoggingService logger, string filePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public string FilePath { get; }

    public async Task<Config> LoadAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
                return new Config();

            string json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            Config config = JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config();

            foreach (string warning in config.Validate())
                await _logger.LogWarnAsync($"config: {warning}").ConfigureAwait(false);

            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            await _logger.LogErrorAsync($"failed to load config from {FilePath}, using defaults: {ex.Message}")
                         .ConfigureAwait(false);
            return new Config();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(Config config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        foreach (string warning in config.Validate())
            await _logger.LogWarnAsync($"config: {warning}").ConfigureAwait(false);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);

            // Write-then-rename: a crash mid-save leaves the previous config intact
            // rather than a truncated file the next launch would fall back on.
            string temporary = FilePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(config, JsonOptions)).ConfigureAwait(false);
            File.Move(temporary, FilePath, overwrite: true);

            await _logger.LogInfoAsync($"config saved to {FilePath}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"failed to save config to {FilePath}: {ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
