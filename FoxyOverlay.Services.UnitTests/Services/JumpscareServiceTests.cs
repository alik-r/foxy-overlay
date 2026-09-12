using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Services.Enums;
using FoxyOverlay.Services.UnitTests.Stubs;


namespace FoxyOverlay.Services.UnitTests.Services;

public class JumpscareServiceTests
{
    private sealed class Harness : IDisposable
    {
        public Harness(Config? config = null, bool alwaysTrigger = false, TimeSpan? playbackTimeout = null)
        {
            Config = config ?? new Config { ChanceX = 10_000, CooldownSeconds = 0 };
            ConfigService = new MockConfigService(Config);
            Logger = new NullLoggingService();
            Stats = new FakeStatsService();
            Player = new FakePlayer();
            Timers = new FakeTimerFactory();

            Service = new JumpscareService(
                ConfigService, Logger, Stats, Player, Timers,
                shouldTrigger: _ => alwaysTrigger,
                playbackTimeout: playbackTimeout);
        }

        public Config Config { get; }
        public MockConfigService ConfigService { get; }
        public NullLoggingService Logger { get; }
        public FakeStatsService Stats { get; }
        public FakePlayer Player { get; }
        public FakeTimerFactory Timers { get; }
        public JumpscareService Service { get; }

        public void Dispose() => Service.Dispose();
    }

    [Fact]
    public async Task StartAsync_StartsRollingOncePerSecond()
    {
        using var harness = new Harness();

        await harness.Service.StartAsync(CancellationToken.None);

        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
        harness.Timers.Current.Period.Should().Be(JumpscareService.RollInterval);
        JumpscareService.RollInterval.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StartAsync_StartsPaused_WhenConfigIsDisabled()
    {
        using var harness = new Harness(new Config { Enabled = false });

        await harness.Service.StartAsync(CancellationToken.None);

        harness.Service.CurrentState.Should().Be(JumpscareState.Paused);
    }

    [Fact]
    public async Task RollOnceAsync_DoesNothing_WhenTheDiceMiss()
    {
        using var harness = new Harness(alwaysTrigger: false);
        await harness.Service.StartAsync(CancellationToken.None);

        bool triggered = await harness.Service.RollOnceAsync();

        triggered.Should().BeFalse();
        harness.Player.PlayCount.Should().Be(0);
        harness.Stats.Rolls.Should().Be(1, "a miss is still a roll");
        harness.Stats.Jumpscares.Should().Be(0);
        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task RollOnceAsync_PlaysAndReturnsToIdle_WhenTheDiceHit()
    {
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        bool triggered = await harness.Service.RollOnceAsync();

        triggered.Should().BeTrue();
        harness.Player.PlayCount.Should().Be(1);
        harness.Stats.Jumpscares.Should().Be(1);
        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task ItTriggersAgainAndAgain()
    {
        // The original bug: the service disposed its timer on the first trigger and
        // relied on the UI calling back to resume, so a jumpscare happened exactly once
        // per launch. This is the regression test for that.
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        for (int i = 0; i < 5; i++)
            await harness.Service.RollOnceAsync();

        harness.Player.PlayCount.Should().Be(5);
        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task AFailingPlayer_DoesNotWedgeTheService()
    {
        // The old event-based design left the state stuck at Playing forever if the
        // handler threw, silently killing every future jumpscare.
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);
        harness.Player.ThrowOnPlay = new InvalidOperationException("no video for you");

        await harness.Service.RollOnceAsync();

        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
        harness.Stats.Errors.Should().Be(1);
        harness.Logger.Errors.Should().ContainSingle(e => e.Contains("no video for you"));

        // ...and the next roll still works.
        harness.Player.ThrowOnPlay = null;
        await harness.Service.RollOnceAsync();
        harness.Player.PlayCount.Should().Be(2);
    }

    [Fact]
    public async Task AHangingPlayer_IsCancelledByTheTimeout()
    {
        using var harness = new Harness(alwaysTrigger: true, playbackTimeout: TimeSpan.FromMilliseconds(150));
        await harness.Service.StartAsync(CancellationToken.None);
        harness.Player.HangUntilCancelled = true;

        await harness.Service.RollOnceAsync();

        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
        harness.Stats.Errors.Should().Be(1);
        harness.Logger.Errors.Should().ContainSingle(e => e.Contains("exceeded"));
    }

    [Fact]
    public async Task ARollDuringPlayback_IsIgnored()
    {
        using var harness = new Harness(alwaysTrigger: true, playbackTimeout: TimeSpan.FromMilliseconds(500));
        await harness.Service.StartAsync(CancellationToken.None);
        harness.Player.HangUntilCancelled = true;

        Task first = harness.Service.RollOnceAsync();
        await Task.Delay(50);

        bool second = await harness.Service.RollOnceAsync();

        second.Should().BeFalse("a scare is already on screen");
        harness.Player.MaxConcurrentPlays.Should().Be(1);

        await first;
    }

    [Fact]
    public async Task TriggerNowAsync_PlaysRegardlessOfTheDice()
    {
        using var harness = new Harness(alwaysTrigger: false);
        await harness.Service.StartAsync(CancellationToken.None);

        await harness.Service.TriggerNowAsync();

        harness.Player.PlayCount.Should().Be(1);
        harness.Stats.Jumpscares.Should().Be(1);
        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task PausedService_NeverTriggers()
    {
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        harness.Service.Pause();
        bool triggered = await harness.Service.RollOnceAsync();

        triggered.Should().BeFalse();
        harness.Player.PlayCount.Should().Be(0);
        harness.Service.CurrentState.Should().Be(JumpscareState.Paused);

        harness.Service.Resume();
        await harness.Service.RollOnceAsync();
        harness.Player.PlayCount.Should().Be(1);
    }

    [Fact]
    public async Task DisabledConfig_SuppressesTriggers()
    {
        using var harness = new Harness(new Config { Enabled = false }, alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        (await harness.Service.RollOnceAsync()).Should().BeFalse();
        harness.Player.PlayCount.Should().Be(0);
    }

    [Fact]
    public async Task ReloadConfigAsync_PicksUpNewSettings_WithoutLosingTheTimer()
    {
        // Saving settings used to dispose and recreate the timer; a failure in there
        // would silently stop the roll loop for the rest of the session.
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);
        int timersBefore = harness.Timers.Created.Count;

        harness.ConfigService.Replace(new Config { ChanceX = 50, CooldownSeconds = 0 });
        await harness.Service.ReloadConfigAsync();

        harness.Service.CurrentConfig.ChanceX.Should().Be(50);
        harness.Timers.Created.Should().HaveCount(timersBefore, "reloading config must not recreate the timer");
        harness.Timers.Current.IsDisposed.Should().BeFalse();

        await harness.Service.RollOnceAsync();
        harness.Player.PlayCount.Should().Be(1);
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // The old StopAsync dereferenced a null timer.
        using var harness = new Harness();

        Func<Task> act = () => harness.Service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_DisposesTheTimerAndStopsTriggering()
    {
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        await harness.Service.StopAsync(CancellationToken.None);

        harness.Timers.Current.IsDisposed.Should().BeTrue();
        harness.Service.CurrentState.Should().Be(JumpscareState.Stopped);
        (await harness.Service.RollOnceAsync()).Should().BeFalse();
        harness.Player.PlayCount.Should().Be(0);
    }

    [Fact]
    public async Task TheTimerCallbackDrivesRolls()
    {
        using var harness = new Harness(alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        harness.Timers.Current.Fire();

        // The callback is fire-and-forget, so give it a moment to land.
        for (int i = 0; i < 50 && harness.Player.PlayCount == 0; i++)
            await Task.Delay(20);

        harness.Player.PlayCount.Should().Be(1);
    }

    [Fact]
    public async Task StateChanged_ReportsTheLifecycle()
    {
        using var harness = new Harness(alwaysTrigger: true);
        var seen = new System.Collections.Concurrent.ConcurrentQueue<JumpscareState>();
        harness.Service.StateChanged += (_, state) => seen.Enqueue(state);

        await harness.Service.StartAsync(CancellationToken.None);
        await harness.Service.RollOnceAsync();

        seen.Should().Contain(JumpscareState.Playing);
        seen.Should().Contain(JumpscareState.Cooldown);
        seen.Last().Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task Cooldown_IsObservedBeforeRollingResumes()
    {
        using var harness = new Harness(new Config { CooldownSeconds = 1 }, alwaysTrigger: true);
        await harness.Service.StartAsync(CancellationToken.None);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        await harness.Service.RollOnceAsync();
        clock.Stop();

        clock.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
        harness.Service.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task Dispose_IsSafeToCallTwice()
    {
        var harness = new Harness();
        await harness.Service.StartAsync(CancellationToken.None);

        harness.Service.Dispose();
        Action act = () => harness.Service.Dispose();

        act.Should().NotThrow();
    }
}
