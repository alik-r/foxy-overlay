using System;
using System.IO;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Core.UnitTests.Stubs;


namespace FoxyOverlay.Core.UnitTests.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempFile;

    public ConfigServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    private static Config DefaultConfig => new Config();

    [Fact]
    public async Task LoadAsync_FileMissing_ReturnsDefaultConfig()
    {
        IConfigService sut = new ConfigService(new NullLoggingService(), _tempFile);
        Config actual = await sut.LoadAsync();

        actual.Should().BeEquivalentTo(DefaultConfig);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsSameValues()
    {
        var original = new Config
        {
            ChanceX = 12345,
            CooldownSeconds = 6,
            VideoPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            IsMuted = true
        };
        
        IConfigService sut = new ConfigService(new NullLoggingService(), _tempFile);
        await sut.SaveAsync(original);
        
        File.Exists(_tempFile).Should().BeTrue();
        
        Config loaded = await sut.LoadAsync();
        loaded.Should().BeEquivalentTo(original,
            options => options.ComparingByMembers<Config>());
    }

    [Fact]
    public async Task LoadAsync_MalformedJson_ReturnsDefaultConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tempFile)!);
        await File.WriteAllTextAsync(_tempFile, "{ not valid json ...");

        IConfigService sut = new ConfigService(new NullLoggingService(), _tempFile);
        Config actual = await sut.LoadAsync();
        
        actual.Should().BeEquivalentTo(DefaultConfig);
    }
}