using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.UnitTests.Stubs;


namespace FoxyOverlay.Core.UnitTests.Services;

public class ConfigServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenNoFileExists()
    {
        using var temp = new TempDirectory();
        var service = new ConfigService(new NullLoggingService(), temp.File("config.json"));

        Config config = await service.LoadAsync();

        config.ChanceX.Should().Be(new Config().ChanceX);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        using var temp = new TempDirectory();
        var service = new ConfigService(new NullLoggingService(), temp.File("config.json"));

        var saved = new Config
        {
            ChanceX = 1234,
            CooldownSeconds = 7,
            PackPath = @"C:\packs\spooky",
            IsMuted = true,
            Enabled = false,
            AllMonitors = false,
            HeightFraction = 0.42,
            StartWithWindows = true
        };
        await service.SaveAsync(saved);

        Config loaded = await service.LoadAsync();

        loaded.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToDefaults_WhenTheFileIsCorrupt()
    {
        using var temp = new TempDirectory();
        string path = temp.File("config.json");
        await File.WriteAllTextAsync(path, "{ this is not json");

        var logger = new NullLoggingService();
        var service = new ConfigService(logger, path);

        Config config = await service.LoadAsync();

        config.ChanceX.Should().Be(new Config().ChanceX, "a broken config must not stop the app starting");
        logger.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_ClampsAndWarns_WhenTheFileHasNonsenseValues()
    {
        using var temp = new TempDirectory();
        string path = temp.File("config.json");
        await File.WriteAllTextAsync(path, """{ "ChanceX": 0, "CooldownSeconds": -9 }""");

        var logger = new NullLoggingService();
        var service = new ConfigService(logger, path);

        Config config = await service.LoadAsync();

        config.ChanceX.Should().Be(Config.MinChanceX);
        config.CooldownSeconds.Should().Be(0);
        logger.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_IsCaseInsensitive()
    {
        using var temp = new TempDirectory();
        string path = temp.File("config.json");
        await File.WriteAllTextAsync(path, """{ "chancex": 555 }""");

        Config config = await new ConfigService(new NullLoggingService(), path).LoadAsync();

        config.ChanceX.Should().Be(555);
    }

    [Fact]
    public async Task SaveAsync_CreatesMissingDirectories()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "nested", "deeper", "config.json");
        var service = new ConfigService(new NullLoggingService(), path);

        await service.SaveAsync(new Config());

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_LeavesNoTemporaryFileBehind()
    {
        using var temp = new TempDirectory();
        string path = temp.File("config.json");

        await new ConfigService(new NullLoggingService(), path).SaveAsync(new Config());

        File.Exists(path + ".tmp").Should().BeFalse("the write-then-rename temp file must be moved, not left");
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        using var temp = new TempDirectory();
        string path = temp.File("config.json");

        await new ConfigService(new NullLoggingService(), path).SaveAsync(new Config { ChanceX = 99 });

        string json = await File.ReadAllTextAsync(path);
        JsonDocument.Parse(json).RootElement.GetProperty("ChanceX").GetInt32().Should().Be(99);
    }
}
