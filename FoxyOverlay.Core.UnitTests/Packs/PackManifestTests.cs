using System;

using FluentAssertions;

using FoxyOverlay.Core.Packs;


namespace FoxyOverlay.Core.UnitTests.Packs;

public class PackManifestTests
{
    [Fact]
    public void ParseFrameRate_HandlesTheNtscRational()
    {
        // ffprobe reports 23.976fps as exactly 24000/1001.
        PackManifest.ParseFrameRate("24000/1001").Should().BeApproximately(23.976, 0.001);
    }

    [Theory]
    [InlineData("30/1", 30.0)]
    [InlineData("60/2", 30.0)]
    [InlineData("25", 25.0)]
    [InlineData("29.97", 29.97)]
    public void ParseFrameRate_AcceptsRationalsAndPlainNumbers(string value, double expected)
    {
        PackManifest.ParseFrameRate(value).Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("24/0")]
    [InlineData("0/1")]
    [InlineData("-24/1")]
    public void ParseFrameRate_RejectsNonsense(string value)
    {
        Action act = () => PackManifest.ParseFrameRate(value);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Duration_IsFrameCountOverFps()
    {
        var manifest = new PackManifest { FrameCount = 25, FrameRate = "24000/1001" };

        manifest.Duration.TotalSeconds.Should().BeApproximately(1.0427, 0.001);
    }

    [Fact]
    public void FrameInterval_IsTheReciprocalOfFps()
    {
        var manifest = new PackManifest { FrameRate = "25/1" };

        manifest.FrameInterval.Should().Be(TimeSpan.FromMilliseconds(40));
    }
}
