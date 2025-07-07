namespace FoxyOverlay.Core.Services.Abstractions;

public interface ILoggingService
{
    Task LogInfoAsync(string message);
    Task LogErrorAsync(string message);
}