using FluentAssertions;
using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.Services.Abstractions;

namespace FoxyOverlay.Core.UnitTests.Services;

public class LoggingServiceTests : IDisposable
{
    private readonly string _logDir;
    private readonly string _logFilePath;
    
    private LoggingService CreateSut() => new LoggingService(_logFilePath);

    public LoggingServiceTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_logDir);
        _logFilePath = Path.Combine(_logDir, "app.log");
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_logDir))
            Directory.Delete(_logDir, true);
    }

    [Theory]
    [InlineData("info")]
    public async Task LogInfoAsync_AppendsInfoLog(string message)
    {
        ILoggingService sut = CreateSut();
        
        await sut.LogInfoAsync(message);
        
        var text = await File.ReadAllTextAsync(_logFilePath);
        text.Should().Contain(message);
    }
    
    [Theory]
    [InlineData("exception")]
    public async Task LogErrorAsync_AppendsErrorLog(string message)
    {
        ILoggingService sut = CreateSut();
        
        await sut.LogErrorAsync(message);
        
        var text = await File.ReadAllTextAsync(_logFilePath);
        text.Should().Contain(message);
    }
}