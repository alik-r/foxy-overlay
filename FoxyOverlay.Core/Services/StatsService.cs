using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Services;

/// <summary>
/// Counters kept in memory and flushed to disk lazily. Rolls happen once a second
/// forever, so writing on every increment would mean 86,400 disk writes a day for
/// a number nobody reads that often.
/// </summary>
public sealed class StatsService : IStatsService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILoggingService _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly object _lock = new object();

    private Stats _stats = new Stats();
    private bool _dirty;

    public StatsService(ILoggingService logger)
        : this(logger, AppPaths.StatsFile)
    {
    }

    public StatsService(ILoggingService logger, string filePath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public Stats Current
    {
        get { lock (_lock) { return _stats.Clone(); } }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            string json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            Stats? loaded = JsonSerializer.Deserialize<Stats>(json, JsonOptions);
            if (loaded == null)
                return;

            lock (_lock)
            {
                _stats = loaded;
                _dirty = false;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            await _logger.LogWarnAsync($"could not read stats from {_filePath}, starting fresh: {ex.Message}")
                         .ConfigureAwait(false);
        }
    }

    public void RecordRoll() => mutate(s => s.RollsTotal++);

    public void RecordJumpscare() => mutate(s =>
    {
        s.JumpscaresTotal++;
        s.LastJumpscareUtc = DateTimeOffset.UtcNow;
    });

    public void RecordError() => mutate(s => s.ErrorsTotal++);

    public async Task FlushAsync()
    {
        Stats snapshot;
        lock (_lock)
        {
            if (!_dirty) return;
            snapshot = _stats.Clone();
            _dirty = false;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            string temporary = _filePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot, JsonOptions)).ConfigureAwait(false);
            File.Move(temporary, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _logger.LogWarnAsync($"could not write stats to {_filePath}: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void mutate(Action<Stats> change)
    {
        lock (_lock)
        {
            change(_stats);
            _dirty = true;
        }
    }
}
