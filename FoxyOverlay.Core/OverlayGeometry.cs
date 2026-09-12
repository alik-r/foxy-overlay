using System;


namespace FoxyOverlay.Core;

/// <summary>Where the overlay goes on a given screen.</summary>
public readonly record struct OverlayRect(int X, int Y, int Width, int Height);

/// <summary>
/// Pure geometry for placing the clip on a screen, kept out of the WPF layer so it
/// can be tested without a display.
/// </summary>
public static class OverlayGeometry
{
    /// <summary>
    /// Centres a <paramref name="contentWidth"/> x <paramref name="contentHeight"/> clip
    /// on a screen at <paramref name="heightFraction"/> of the screen height, preserving
    /// aspect ratio and shrinking to fit if that would overflow the screen width.
    /// </summary>
    public static OverlayRect Fit(
        int screenX, int screenY, int screenWidth, int screenHeight,
        int contentWidth, int contentHeight,
        double heightFraction)
    {
        if (screenWidth <= 0) throw new ArgumentOutOfRangeException(nameof(screenWidth));
        if (screenHeight <= 0) throw new ArgumentOutOfRangeException(nameof(screenHeight));
        if (contentWidth <= 0) throw new ArgumentOutOfRangeException(nameof(contentWidth));
        if (contentHeight <= 0) throw new ArgumentOutOfRangeException(nameof(contentHeight));

        if (double.IsNaN(heightFraction) || heightFraction <= 0)
            heightFraction = Config.MinHeightFraction;

        double aspect = (double)contentWidth / contentHeight;
        double height = screenHeight * heightFraction;
        double width = height * aspect;

        if (width > screenWidth)
        {
            width = screenWidth;
            height = width / aspect;
        }

        int w = Math.Max(1, (int)Math.Round(width));
        int h = Math.Max(1, (int)Math.Round(height));

        return new OverlayRect(
            screenX + (screenWidth - w) / 2,
            screenY + (screenHeight - h) / 2,
            w, h);
    }
}
