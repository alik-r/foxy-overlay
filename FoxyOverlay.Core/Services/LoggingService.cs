using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Services;

public class LoggingService : ILoggingService, IDisposable
{
    private enum LogLevel
    {
        Info,
        Error
    }
    
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public LoggingService(string? logFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            _logFilePath = logFilePath;
            string? dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "FoxyOverlay");
            Directory.CreateDirectory(folder);
            _logFilePath = Path.Combine(folder, "app.log");
        }
    }
    
    public void Dispose() => _semaphore.Dispose();

    public async Task LogInfoAsync(string message) => await LogAsync(LogLevel.Info, message);

    public async Task LogErrorAsync(string message) => await LogAsync(LogLevel.Error, message);

    private async Task LogAsync(LogLevel level, string message)
    {
        var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"{ts} {level.ToString().ToUpperInvariant()}: {message}{Environment.NewLine}";

        await _semaphore.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<string>> ReadLogsAsync(int maxLines = 500)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_logFilePath))
                return Array.Empty<string>();

            using var fs = new FileStream(
                _logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            using var reader = new StreamReader(fs);

            var buffer = new Queue<string>(maxLines);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                buffer.Enqueue(line);
                if (buffer.Count > maxLines)
                    buffer.Dequeue();
            }

            return buffer.ToArray();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}