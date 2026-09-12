using FluentAssertions;

using FoxyOverlay.Core;


namespace FoxyOverlay.Core.UnitTests;

public class OverlayGeometryTests
{
    [Fact]
    public void Fit_CentresTheClipOnTheScreen()
    {
        OverlayRect rect = OverlayGeometry.Fit(0, 0, 1920, 1200, 946, 720, 0.55);

        rect.Height.Should().Be(660, "0.55 of a 1200px-tall screen");
        rect.Width.Should().Be(867, "946:720 aspect at 660px tall");
        rect.X.Should().Be((1920 - 867) / 2);
        rect.Y.Should().Be((1200 - 660) / 2);
    }

    [Fact]
    public void Fit_OffsetsBySecondaryMonitorOrigin()
    {
        // A 1280x800 monitor to the right of and slightly above the primary.
        OverlayRect rect = OverlayGeometry.Fit(1920, -200, 1280, 800, 946, 720, 0.5);

        rect.Height.Should().Be(400);
        rect.Width.Should().Be(526);
        rect.X.Should().Be(1920 + (1280 - 526) / 2, "centred within the monitor, not the desktop");
        rect.Y.Should().Be(-200 + (800 - 400) / 2);
    }

    [Fact]
    public void Fit_PreservesAspectRatio()
    {
        OverlayRect rect = OverlayGeometry.Fit(0, 0, 1920, 1080, 946, 720, 0.8);

        ((double)rect.Width / rect.Height).Should().BeApproximately(946.0 / 720.0, 0.01);
    }

    [Fact]
    public void Fit_ShrinksToFitAWideClipOnANarrowScreen()
    {
        // A 32:9 clip at full screen height would be far wider than the screen.
        OverlayRect rect = OverlayGeometry.Fit(0, 0, 1000, 1000, 3200, 900, 1.0);

        rect.Width.Should().BeLessThanOrEqualTo(1000);
        rect.X.Should().BeGreaterThanOrEqualTo(0, "shrinking to fit must not push it off-screen");
    }

    [Fact]
    public void Fit_NeverProducesAZeroSizedWindow()
    {
        OverlayRect rect = OverlayGeometry.Fit(0, 0, 1920, 1080, 946, 720, 0.0001);

        rect.Width.Should().BeGreaterThan(0);
        rect.Height.Should().BeGreaterThan(0);
    }
}
