using System;

using FluentAssertions;

using FoxyOverlay.Core;


namespace FoxyOverlay.Core.UnitTests;

public class ProbabilityTests
{
    [Fact]
    public void SecondsUntilConfidence_MatchesTheClosedForm()
    {
        // P(no trigger in n seconds) = (1 - 1/10000)^n; solve for n at 1% remaining.
        double expected = Math.Ceiling(Math.Log(0.01) / Math.Log(1.0 - 1.0 / 10_000));

        Probability.SecondsUntilConfidence(10_000).Should().Be(expected);
    }

    [Fact]
    public void SecondsUntilConfidence_GrowsWithLongerOdds()
    {
        double small = Probability.SecondsUntilConfidence(100);
        double large = Probability.SecondsUntilConfidence(10_000);

        large.Should().BeGreaterThan(small);
    }

    [Fact]
    public void SecondsUntilConfidence_ForTheDefaultOdds_IsAboutThirteenHours()
    {
        double seconds = Probability.SecondsUntilConfidence(10_000);

        TimeSpan.FromSeconds(seconds).TotalHours.Should().BeApproximately(12.79, 0.1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public void SecondsUntilConfidence_RejectsOddsBelowTheMinimum(int chance)
    {
        Action act = () => Probability.SecondsUntilConfidence(chance);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void SecondsUntilConfidence_RejectsImpossibleConfidence(double confidence)
    {
        Action act = () => Probability.SecondsUntilConfidence(10_000, confidence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Humanize_DropsZeroComponents()
    {
        Probability.Humanize(TimeSpan.FromSeconds(3_600)).Should().Be("1 hour");
    }

    [Fact]
    public void Humanize_PluralisesAndJoins()
    {
        Probability.Humanize(new TimeSpan(2, 3, 4, 0)).Should().Be("2 days, 3 hours, 4 minutes");
    }

    [Fact]
    public void Humanize_KeepsAtMostThreeComponents()
    {
        string text = Probability.Humanize(new TimeSpan(1, 2, 3, 4));

        text.Split(',').Should().HaveCount(3);
    }

    [Fact]
    public void Humanize_HandlesSubSecondDurations()
    {
        Probability.Humanize(TimeSpan.FromMilliseconds(200)).Should().Be("less than a second");
    }
}
