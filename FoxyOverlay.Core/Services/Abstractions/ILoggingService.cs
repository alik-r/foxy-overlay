using System.Collections.Generic;
using System.Threading.Tasks;


namespace FoxyOverlay.Core.Services.Abstractions;

public interface ILoggingService
{
    Task LogInfoAsync(string message);
    Task LogErrorAsync(string message);
    Task<IEnumerable<string>> ReadLogsAsync(int maxLines = 500);
}