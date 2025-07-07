using FoxyOverlay.Core.Services.Abstractions;

namespace FoxyOverlay.Core.UnitTests.Stubs;

public class NullLoggingService : ILoggingService
{
    public Task LogInfoAsync(string message) => Task.CompletedTask;
    public Task LogErrorAsync(string message) => Task.CompletedTask;
}