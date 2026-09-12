using System.IO;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services;
using FoxyOverlay.Core.UnitTests.Stubs;


namespace FoxyOverlay.Core.UnitTests.Services;

public class StatsServiceTests
{
    private static StatsService Create(TempDirectory temp) =>
        new StatsService(new NullLoggingService(), temp.File("stats.json"));

    [Fact]
    public void Current_StartsAtZero()
    {
        using var temp = new TempDirectory();

        Stats stats = Create(temp).Current;

        stats.JumpscaresTotal.Should().Be(0);
        stats.RollsTotal.Should().Be(0);
        stats.LastJumpscareUtc.Should().BeNull();
    }

    [Fact]
    public void RecordJumpscare_IncrementsAndStampsTheTime()
    {
        using var temp = new TempDirectory();
        var service = Create(temp);

        service.RecordJumpscare();

        service.Current.JumpscaresTotal.Should().Be(1);
        service.Current.LastJumpscareUtc.Should().NotBeNull();
    }

    [Fact]
    public void Current_ReturnsASnapshot_NotALiveReference()
    {
        using var temp = new TempDirectory();
        var service = Create(temp);

        Stats before = service.Current;
        service.RecordRoll();

        before.RollsTotal.Should().Be(0, "an already-taken snapshot must not change underneath the caller");
        service.Current.RollsTotal.Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_PersistsAcrossInstances()
    {
        using var temp = new TempDirectory();
        var service = Create(temp);

        service.RecordRoll();
        service.RecordRoll();
        service.RecordJumpscare();
        await service.FlushAsync();

        var reloaded = Create(temp);
        await reloaded.LoadAsync();

        reloaded.Current.RollsTotal.Should().Be(2);
        reloaded.Current.JumpscaresTotal.Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_WritesNothing_WhenNothingChanged()
    {
        using var temp = new TempDirectory();
        var service = Create(temp);

        await service.FlushAsync();

        File.Exists(temp.File("stats.json")).Should().BeFalse("an unchanged counter set need not touch the disk");
    }

    [Fact]
    public async Task LoadAsync_StartsFresh_WhenTheFileIsCorrupt()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("stats.json"), "not json at all");

        var logger = new NullLoggingService();
        var service = new StatsService(logger, temp.File("stats.json"));
        await service.LoadAsync();

        service.Current.JumpscaresTotal.Should().Be(0);
        logger.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void ObservedOddsDenominator_ReportsTheRealisedRate()
    {
        var stats = new Stats { RollsTotal = 20_000, JumpscaresTotal = 2 };

        stats.ObservedOddsDenominator.Should().Be(10_000);
    }

    [Fact]
    public void ObservedOddsDenominator_IsNaN_BeforeTheFirstScare()
    {
        new Stats { RollsTotal = 500 }.ObservedOddsDenominator.Should().Be(double.NaN);
    }
}
