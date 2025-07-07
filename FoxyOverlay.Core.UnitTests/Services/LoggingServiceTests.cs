using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
    
    [Fact]
    public async Task ReadLogsAsync_NoFile_ReturnsEmpty()
    {
        ILoggingService sut = CreateSut();

        var result = await sut.ReadLogsAsync();

        result.Should().BeEmpty();
    }
    
    [Fact]
    public async Task ReadLogsAsync_FewerThanMaxLines_ReturnsAllLines()
    {
        // arrange
        var lines = new[]
        {
            "Line one",
            "Line two",
            "Line three"
        };
        File.WriteAllLines(_logFilePath, lines);

        ILoggingService sut = CreateSut();

        // act
        var result = await sut.ReadLogsAsync(maxLines: 5);

        // assert
        var enumerable = result as string[] ?? result.ToArray();
        enumerable.Should().HaveCount(lines.Length);
        enumerable.Should().BeEquivalentTo(lines, options => options.WithStrictOrdering());
    }
    
    [Theory]
    [InlineData(3, 5)]
    [InlineData(1, 10)]
    public async Task ReadLogsAsync_MoreThanMaxLines_ReturnsLastMaxLines(int maxLines, int totalLines)
    {
        // arrange
        var all = Enumerable.Range(1, totalLines)
            .Select(i => $"Log {i}")
            .ToArray();
        File.WriteAllLines(_logFilePath, all);

        ILoggingService sut = CreateSut();

        // act
        var result = (await sut.ReadLogsAsync(maxLines)).ToArray();

        // assert
        var expected = all.Skip(totalLines - maxLines).ToArray();
        result.Should().HaveCount(maxLines)
            .And.BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }
}