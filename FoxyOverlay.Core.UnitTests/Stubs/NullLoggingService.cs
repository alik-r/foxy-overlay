using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.UnitTests.Stubs;

/// <summary>Captures log lines in memory so tests can assert on them.</summary>
public sealed class NullLoggingService : ILoggingService
{
    private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

    public IReadOnlyList<string> Lines => _lines.ToArray();
    public IEnumerable<string> Warnings => _lines.Where(l => l.StartsWith("WARN:"));
    public IEnumerable<string> Errors => _lines.Where(l => l.StartsWith("ERROR:"));

    public Task LogInfoAsync(string message)
    {
        _lines.Enqueue("INFO:" + message);
        return Task.CompletedTask;
    }

    public Task LogWarnAsync(string message)
    {
        _lines.Enqueue("WARN:" + message);
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message)
    {
        _lines.Enqueue("ERROR:" + message);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ReadLogsAsync(int maxLines = 500) =>
        Task.FromResult<IEnumerable<string>>(_lines.ToArray());
}
