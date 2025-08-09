using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using FoxyOverlay.Core;
using FoxyOverlay.Core.UnitTests.Stubs;
using FoxyOverlay.Services.Enums;
using FoxyOverlay.Services.UnitTests.Stubs;
using FoxyOverlay.Services.Utils.Abstractions;


namespace FoxyOverlay.Services.UnitTests.Services;

public class JumpscareServiceTests
{
    [Fact]
    public async Task DoesNotTrigger_WhenPredicateFalse()
    {
        Config config = new Config { ChanceX = 1 };
        ITimerFactory timerFactory = new FakeTimerFactory();
        JumpscareService sut =
            new JumpscareService(new MockConfigService(config), new NullLoggingService(), timerFactory, () => false);

        await sut.StartAsync(CancellationToken.None);
        bool triggered = false;
        sut.JumpscareTriggered += (_, _) => triggered = true;

        sut.TriggerTick();

        triggered.Should().BeFalse();
        sut.CurrentState.Should().Be(JumpscareState.Idle);
    }

    [Fact]
    public async Task TriggersOnce_WhenPredicateTrue()
    {
        Config config = new Config { ChanceX = 1 };
        ITimerFactory timerFactory = new FakeTimerFactory();
        JumpscareService sut =
            new JumpscareService(new MockConfigService(config), new NullLoggingService(), timerFactory, () => false);
        
        await sut.StartAsync(CancellationToken.None);

        ushort count = 0;
        sut.JumpscareTriggered += (_, _) => count++;
        
        sut.TriggerTick();
        sut.TriggerTick(); // should be ignored
        
        count.Should().Be(1);
        sut.CurrentState.Should().Be(JumpscareState.Playing);
    }

    [Fact]
    public async Task RestartAsync_ResetsToIdle()
    {
        Config config = new Config { ChanceX = 1 };
        ITimerFactory timerFactory = new FakeTimerFactory();
        JumpscareService sut =
            new JumpscareService(new MockConfigService(config), new NullLoggingService(), timerFactory, () => false);
        
        await sut.StartAsync(CancellationToken.None);
        
        ushort count = 0;
        sut.JumpscareTriggered += (_, _) => count++;
        
        sut.TriggerTick();

        await sut.ResumeAsync();
        sut.CurrentState.Should().Be(JumpscareState.Idle);
        
        sut.TriggerTick();
        count.Should().Be(2);
    }

    [Fact]
    public async Task StopAsync_DisableTriggers()
    {
        Config config = new Config { ChanceX = 1 };
        ITimerFactory timerFactory = new FakeTimerFactory();
        JumpscareService sut =
            new JumpscareService(new MockConfigService(config), new NullLoggingService(), timerFactory, () => false);
        
        await sut.StartAsync(CancellationToken.None);
        
        ushort count = 0;
        sut.JumpscareTriggered += (_, _) => count++;
        
        sut.TriggerTick();
        await sut.StopAsync(CancellationToken.None);
        sut.TriggerTick();
        
        count.Should().Be(1);
    }
}