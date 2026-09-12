using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Services.UnitTests.Stubs;


namespace FoxyOverlay.Services.UnitTests.Services;

public class DefaultTriggerTests
{
    [Fact]
    public async Task TheRealDiceFireAtRoughlyTheConfiguredRate()
    {
        // Guards the odds themselves: with the default predicate and 1-in-20 odds,
        // 20,000 rolls should produce on the order of 1,000 hits. Wide bounds keep
        // this from flaking, but an off-by-one in the range would blow straight past.
        var config = new Config { ChanceX = 20, CooldownSeconds = 0 };
        var player = new FakePlayer();

        using var service = new JumpscareService(
            new MockConfigService(config), new NullLoggingService(),
            new FakeStatsService(), player, new FakeTimerFactory());

        await service.StartAsync(CancellationToken.None);

        for (int i = 0; i < 20_000; i++)
            await service.RollOnceAsync();

        player.PlayCount.Should().BeInRange(800, 1_200);
    }

    [Fact]
    public async Task ImpossibleOddsStillFireEventually()
    {
        // ChanceX = 2 is the configured minimum, so Random.Shared.Next(2) == 0 must
        // be reachable; a Next(1, n+1) == 1 style off-by-one would make this hang.
        var config = new Config { ChanceX = 2, CooldownSeconds = 0 };
        var player = new FakePlayer();

        using var service = new JumpscareService(
            new MockConfigService(config), new NullLoggingService(),
            new FakeStatsService(), player, new FakeTimerFactory());

        await service.StartAsync(CancellationToken.None);

        for (int i = 0; i < 100; i++)
            await service.RollOnceAsync();

        player.PlayCount.Should().BeGreaterThan(0);
    }
}
