using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.Services;

/// <summary>
/// Append-only file log with size-based rotation. The app runs unattended for days at
/// a time, so an unbounded log file is a real (if slow) way to fill someone's disk.
/// </summary>
public sealed class LoggingService : ILoggingService, IDisposable
{
    public const long DefaultMaxBytes = 1024 * 1024;
    public const int DefaultRetainedFiles = 3;

    private enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    private readonly string _logFilePath;
    private readonly long _maxBytes;
    private readonly int _retainedFiles;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private bool _disposed;

    public LoggingService()
        : this(null)
    {
    }

    public LoggingService(string? logFilePath, long maxBytes = DefaultMaxBytes, int retainedFiles = DefaultRetainedFiles)
    {
        _logFilePath = string.IsNullOrWhiteSpace(logFilePath) ? AppPaths.LogFile : logFilePath;
        _maxBytes = maxBytes > 0 ? maxBytes : DefaultMaxBytes;
        _retainedFiles = retainedFiles >= 0 ? retainedFiles : DefaultRetainedFiles;

        string? directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    public string FilePath => _logFilePath;

    public Task LogInfoAsync(string message) => logAsync(LogLevel.Info, message);

    public Task LogWarnAsync(string message) => logAsync(LogLevel.Warn, message);

    public Task LogErrorAsync(string message) => logAsync(LogLevel.Error, message);

    public async Task<IEnumerable<string>> ReadLogsAsync(int maxLines = 500)
    {
        if (maxLines <= 0) return Array.Empty<string>();

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_logFilePath))
                return Array.Empty<string>();

            using var stream = new FileStream(
                _logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 4096, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream);

            var buffer = new Queue<string>(maxLines);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }

    private async Task logAsync(LogLevel level, string message)
    {
        if (_disposed) return;

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string line = $"{timestamp} {level.ToString().ToUpperInvariant()}: {message}{Environment.NewLine}";

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            rotateIfNeeded();
            await File.AppendAllTextAsync(_logFilePath, line).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Logging must never take the app down with it.
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Shifts app.log -> app.1.log -> app.2.log ..., discarding the oldest.</summary>
    private void rotateIfNeeded()
    {
        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length < _maxBytes)
            return;

        if (_retainedFiles == 0)
        {
            File.Delete(_logFilePath);
            return;
        }

        string directory = Path.GetDirectoryName(_logFilePath) ?? ".";
        string stem = Path.GetFileNameWithoutExtension(_logFilePath);
        string extension = Path.GetExtension(_logFilePath);
        string archive(int index) => Path.Combine(directory, $"{stem}.{index}{extension}");

        string oldest = archive(_retainedFiles);
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (int i = _retainedFiles - 1; i >= 1; i--)
        {
            if (File.Exists(archive(i)))
                File.Move(archive(i), archive(i + 1), overwrite: true);
        }

        File.Move(_logFilePath, archive(1), overwrite: true);
    }
}
