using System;


namespace FoxyOverlay.Core;

/// <summary>
/// Lifetime counters shown in the settings window. Not telemetry: this never
/// leaves the machine, it just answers "how many times has it got me?".
/// </summary>
public sealed class Stats
{
    public long RollsTotal { get; set; }
    public long JumpscaresTotal { get; set; }
    public long ErrorsTotal { get; set; }
    public DateTimeOffset FirstRunUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastJumpscareUtc { get; set; }

    /// <summary>
    /// Observed rate against the configured odds. Useful as a sanity check that the
    /// roll really is one-in-N per second, and mildly interesting on its own.
    /// </summary>
    public double ObservedOddsDenominator =>
        JumpscaresTotal > 0 ? (double)RollsTotal / JumpscaresTotal : double.NaN;

    public Stats Clone() => (Stats)MemberwiseClone();
}
