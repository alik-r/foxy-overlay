using System;
using System.Collections.Generic;


namespace FoxyOverlay.Core;

/// <summary>
/// The "how long until it gets me?" maths behind the settings window.
/// </summary>
public static class Probability
{
    /// <summary>
    /// Seconds after which the cumulative chance of at least one trigger reaches
    /// <paramref name="confidence"/>, given one-in-<paramref name="chanceX"/> odds per second.
    /// </summary>
    public static double SecondsUntilConfidence(int chanceX, double confidence = 0.99)
    {
        if (chanceX < Config.MinChanceX)
            throw new ArgumentOutOfRangeException(nameof(chanceX), chanceX, $"must be >= {Config.MinChanceX}");
        if (confidence <= 0 || confidence >= 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "must be strictly between 0 and 1");

        double probMiss = 1.0 - (1.0 / chanceX);
        return Math.Ceiling(Math.Log(1.0 - confidence) / Math.Log(probMiss));
    }

    /// <summary>Mean seconds between triggers: the expectation of a geometric distribution.</summary>
    public static double MeanSecondsBetween(int chanceX) => chanceX;

    /// <summary>Renders a duration as "2 days, 3 hours, 4 minutes", dropping zero components.</summary>
    public static string Humanize(TimeSpan time)
    {
        if (time.TotalSeconds < 1)
            return "less than a second";

        var parts = new List<string>();
        void add(int value, string unit)
        {
            if (value > 0) parts.Add($"{value} {unit}{(value > 1 ? "s" : "")}");
        }

        add((int)(time.TotalDays / 365), "year");
        add(time.Days % 365, "day");
        add(time.Hours, "hour");
        add(time.Minutes, "minute");
        add(time.Seconds, "second");

        // Three components is plenty; "1 year, 2 days, 3 hours" reads better than the full tail.
        if (parts.Count > 3) parts.RemoveRange(3, parts.Count - 3);
        return string.Join(", ", parts);
    }
}
