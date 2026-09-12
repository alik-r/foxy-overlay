using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.UnitTests.Stubs;


namespace FoxyOverlay.Core.UnitTests.Services;

public class LoggingServiceTests
{
    [Fact]
    public async Task LogInfoAsync_WritesALineWithLevelAndMessage()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"));

        await logger.LogInfoAsync("hello");

        string contents = await File.ReadAllTextAsync(temp.File("app.log"));
        contents.Should().Contain("INFO").And.Contain("hello");
    }

    [Fact]
    public async Task EachLevelIsLabelled()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"));

        await logger.LogInfoAsync("i");
        await logger.LogWarnAsync("w");
        await logger.LogErrorAsync("e");

        string contents = await File.ReadAllTextAsync(temp.File("app.log"));
        contents.Should().Contain("INFO: i").And.Contain("WARN: w").And.Contain("ERROR: e");
    }

    [Fact]
    public async Task ReadLogsAsync_ReturnsOnlyTheLastNLines()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"));

        for (int i = 0; i < 50; i++)
            await logger.LogInfoAsync($"line {i}");

        var lines = (await logger.ReadLogsAsync(maxLines: 10)).ToList();

        lines.Should().HaveCount(10);
        lines.Last().Should().Contain("line 49");
    }

    [Fact]
    public async Task ReadLogsAsync_IsEmptyBeforeAnythingIsLogged()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"));

        (await logger.ReadLogsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Rotates_OnceTheFileExceedsItsSizeLimit()
    {
        // The app runs unattended for days; an unbounded log slowly eats the disk.
        using var temp = new TempDirectory();
        string path = temp.File("app.log");
        using var logger = new LoggingService(path, maxBytes: 512, retainedFiles: 2);

        for (int i = 0; i < 60; i++)
            await logger.LogInfoAsync(new string('x', 60));

        File.Exists(temp.File("app.1.log")).Should().BeTrue("the previous log becomes app.1.log");
        new FileInfo(path).Length.Should().BeLessThan(2048, "the live log is truncated by rotation");
    }

    [Fact]
    public async Task Rotation_DiscardsArchivesBeyondTheRetentionLimit()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"), maxBytes: 256, retainedFiles: 2);

        for (int i = 0; i < 200; i++)
            await logger.LogInfoAsync(new string('y', 60));

        File.Exists(temp.File("app.3.log")).Should().BeFalse("only 2 archives are retained");
        Directory.GetFiles(temp.Path, "app*.log").Length.Should().BeLessThan(4, "app.log plus at most 2 archives");
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotInterleaveOrThrow()
    {
        using var temp = new TempDirectory();
        using var logger = new LoggingService(temp.File("app.log"));

        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => logger.LogInfoAsync($"m{i}")));

        var lines = (await logger.ReadLogsAsync(1000)).ToList();
        lines.Should().HaveCount(100);
        lines.Should().OnlyContain(line => line.Contains("INFO"));
    }
}
