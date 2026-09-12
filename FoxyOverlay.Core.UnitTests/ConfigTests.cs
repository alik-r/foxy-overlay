using FluentAssertions;

using FoxyOverlay.Core;


namespace FoxyOverlay.Core.UnitTests;

public class ConfigTests
{
    [Fact]
    public void Defaults_MatchTheTerrariaMod()
    {
        var config = new Config();

        config.ChanceX.Should().Be(10_000, "the mod rolls one-in-ten-thousand every second");
        config.Enabled.Should().BeTrue();
        config.CooldownSeconds.Should().Be(3);
    }

    [Fact]
    public void Validate_LeavesAGoodConfigAlone()
    {
        var config = new Config { ChanceX = 5_000, CooldownSeconds = 10, HeightFraction = 0.5 };

        config.Validate().Should().BeEmpty();
        config.ChanceX.Should().Be(5_000);
        config.CooldownSeconds.Should().Be(10);
        config.HeightFraction.Should().Be(0.5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-50)]
    public void Validate_ClampsChanceUpToTheMinimum(int chance)
    {
        // 1-in-1 would fire every single second, and 1-in-0 would divide by zero in
        // the "how long until it gets me" maths.
        var config = new Config { ChanceX = chance };

        config.Validate().Should().ContainSingle();
        config.ChanceX.Should().Be(Config.MinChanceX);
    }

    [Fact]
    public void Validate_ClampsChanceDownToTheMaximum()
    {
        var config = new Config { ChanceX = int.MaxValue };

        config.Validate().Should().ContainSingle();
        config.ChanceX.Should().Be(Config.MaxChanceX);
    }

    [Fact]
    public void Validate_ClampsNegativeCooldown()
    {
        var config = new Config { CooldownSeconds = -5 };

        config.Validate().Should().ContainSingle();
        config.CooldownSeconds.Should().Be(0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Validate_ClampsUnusableHeightFraction(double fraction)
    {
        var config = new Config { HeightFraction = fraction };

        config.Validate().Should().ContainSingle();
        config.HeightFraction.Should().Be(Config.MinHeightFraction);
    }

    [Fact]
    public void Validate_ReportsEveryProblemAtOnce()
    {
        var config = new Config { ChanceX = 0, CooldownSeconds = -1, HeightFraction = 99 };

        config.Validate().Should().HaveCount(3);
    }

    [Fact]
    public void Clone_DoesNotShareState()
    {
        var config = new Config { ChanceX = 42 };
        Config copy = config.Clone();

        copy.ChanceX = 7;

        config.ChanceX.Should().Be(42);
    }
}
