using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Core.UnitTests.Stubs;

public class NullLoggingService : ILoggingService
{
    public Task LogInfoAsync(string message) => Task.CompletedTask;
    public Task LogErrorAsync(string message) => Task.CompletedTask;
    public Task<IEnumerable<string>> ReadLogsAsync(int maxLines = 500) => Task.FromResult(Enumerable.Empty<string>());
}