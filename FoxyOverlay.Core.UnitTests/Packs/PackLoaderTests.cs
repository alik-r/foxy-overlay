using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Packs;
using FoxyOverlay.Core.UnitTests.Stubs;


namespace FoxyOverlay.Core.UnitTests.Packs;

public class PackLoaderTests
{
    /// <summary>Builds a pack directory with placeholder frame files.</summary>
    private static async Task<string> WritePack(
        TempDirectory temp, int frameCount = 3, string? audioFile = "audio.wav", bool writeFrames = true)
    {
        string directory = Path.Combine(temp.Path, "pack");
        Directory.CreateDirectory(directory);

        var manifest = new PackManifest
        {
            Name = "test",
            Width = 100,
            Height = 80,
            FrameCount = frameCount,
            FrameRate = "24/1",
            FramePattern = "frame_{0:D3}.png",
            AudioFile = audioFile
        };

        await File.WriteAllTextAsync(
            Path.Combine(directory, PackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));

        if (writeFrames)
        {
            for (int i = 0; i < frameCount; i++)
                await File.WriteAllTextAsync(Path.Combine(directory, $"frame_{i:D3}.png"), "not-really-a-png");
        }

        if (audioFile != null)
            await File.WriteAllTextAsync(Path.Combine(directory, audioFile), "not-really-a-wav");

        return directory;
    }

    [Fact]
    public async Task LoadAsync_ResolvesEveryFramePathInOrder()
    {
        using var temp = new TempDirectory();
        string directory = await WritePack(temp, frameCount: 3);

        Pack pack = await new PackLoader().LoadAsync(directory);

        pack.FramePaths.Should().HaveCount(3);
        pack.FramePaths[0].Should().EndWith("frame_000.png");
        pack.FramePaths[2].Should().EndWith("frame_002.png");
        pack.HasAudio.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_HandlesASilentPack()
    {
        using var temp = new TempDirectory();
        string directory = await WritePack(temp, audioFile: null);

        Pack pack = await new PackLoader().LoadAsync(directory);

        pack.HasAudio.Should().BeFalse();
        pack.AudioPath.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenADeclaredFrameIsMissing()
    {
        // Better to fail loudly at startup than to die halfway through a jumpscare.
        using var temp = new TempDirectory();
        string directory = await WritePack(temp, frameCount: 3);
        File.Delete(Path.Combine(directory, "frame_001.png"));

        Func<Task> act = () => new PackLoader().LoadAsync(directory);

        (await act.Should().ThrowAsync<PackException>()).WithMessage("*frame_001.png*");
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenTheDeclaredAudioIsMissing()
    {
        using var temp = new TempDirectory();
        string directory = await WritePack(temp);
        File.Delete(Path.Combine(directory, "audio.wav"));

        Func<Task> act = () => new PackLoader().LoadAsync(directory);

        await act.Should().ThrowAsync<PackException>();
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenTheDirectoryDoesNotExist()
    {
        Func<Task> act = () => new PackLoader().LoadAsync("/definitely/not/here");

        (await act.Should().ThrowAsync<PackException>()).WithMessage("*does not exist*");
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenTheManifestIsMissing()
    {
        using var temp = new TempDirectory();

        Func<Task> act = () => new PackLoader().LoadAsync(temp.Path);

        (await act.Should().ThrowAsync<PackException>()).WithMessage("*pack.json*");
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenTheManifestIsCorrupt()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, PackLoader.ManifestFileName), "{ broken");

        Func<Task> act = () => new PackLoader().LoadAsync(temp.Path);

        await act.Should().ThrowAsync<PackException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task LoadAsync_Throws_OnANonPositiveFrameCount(int frameCount)
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Path, PackLoader.ManifestFileName),
            JsonSerializer.Serialize(new PackManifest { FrameCount = frameCount, Width = 1, Height = 1 }));

        Func<Task> act = () => new PackLoader().LoadAsync(temp.Path);

        (await act.Should().ThrowAsync<PackException>()).WithMessage("*frameCount*");
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenFramePatternHasNoPlaceholder()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Path, PackLoader.ManifestFileName),
            JsonSerializer.Serialize(new PackManifest
            {
                FrameCount = 1, Width = 1, Height = 1, FramePattern = "frame.png"
            }));

        Func<Task> act = () => new PackLoader().LoadAsync(temp.Path);

        (await act.Should().ThrowAsync<PackException>()).WithMessage("*framePattern*");
    }

    [Fact]
    public void IsPackDirectory_RecognisesAManifest()
    {
        using var temp = new TempDirectory();
        PackLoader.IsPackDirectory(temp.Path).Should().BeFalse();

        File.WriteAllText(Path.Combine(temp.Path, PackLoader.ManifestFileName), "{}");

        PackLoader.IsPackDirectory(temp.Path).Should().BeTrue();
    }

    [Fact]
    public void ResolveDirectory_UsesTheBundledPack_WhenNoneIsConfigured()
    {
        PackLoader.ResolveDirectory(new Config { PackPath = null })
                  .Should().Be(PackLoader.BundledPackDirectory);
        PackLoader.ResolveDirectory(new Config { PackPath = "   " })
                  .Should().Be(PackLoader.BundledPackDirectory);
    }

    [Fact]
    public void ResolveDirectory_UsesTheConfiguredPath_WhenSet()
    {
        using var temp = new TempDirectory();

        PackLoader.ResolveDirectory(new Config { PackPath = temp.Path }).Should().Be(temp.Path);
    }
}
