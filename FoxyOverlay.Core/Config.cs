using System;
using System.Collections.Generic;


namespace FoxyOverlay.Core;

/// <summary>
/// User-facing settings, persisted as JSON under %AppData%\FoxyOverlay\config.json.
/// </summary>
public sealed class Config
{
    public const int MinChanceX = 2;
    public const int MaxChanceX = 100_000_000;
    public const int MaxCooldownSeconds = 24 * 60 * 60;
    public const double MinHeightFraction = 0.1;
    public const double MaxHeightFraction = 1.0;

    /// <summary>One-in-<c>ChanceX</c> odds, rolled once per second.</summary>
    public int ChanceX { get; set; } = 10_000;

    /// <summary>Quiet period after a jumpscare finishes, before rolling resumes.</summary>
    public int CooldownSeconds { get; set; } = 3;

    /// <summary>Directory of a baked frame pack. Null or empty uses the bundled Foxy pack.</summary>
    public string? PackPath { get; set; }

    public bool IsMuted { get; set; }

    /// <summary>Master switch; when false the roll timer keeps running but never fires.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Show the overlay on every monitor, or only the primary one.</summary>
    public bool AllMonitors { get; set; } = true;

    /// <summary>Overlay height as a fraction of the screen height.</summary>
    public double HeightFraction { get; set; } = 0.85;

    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Clamps out-of-range values in place and reports what was corrected. A config
    /// file edited by hand is the most likely source of nonsense, and silently
    /// clamping beats refusing to start.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var warnings = new List<string>();

        if (ChanceX < MinChanceX)
        {
            warnings.Add($"ChanceX {ChanceX} below minimum {MinChanceX}; clamped.");
            ChanceX = MinChanceX;
        }
        else if (ChanceX > MaxChanceX)
        {
            warnings.Add($"ChanceX {ChanceX} above maximum {MaxChanceX}; clamped.");
            ChanceX = MaxChanceX;
        }

        if (CooldownSeconds < 0)
        {
            warnings.Add($"CooldownSeconds {CooldownSeconds} is negative; clamped to 0.");
            CooldownSeconds = 0;
        }
        else if (CooldownSeconds > MaxCooldownSeconds)
        {
            warnings.Add($"CooldownSeconds {CooldownSeconds} above maximum {MaxCooldownSeconds}; clamped.");
            CooldownSeconds = MaxCooldownSeconds;
        }

        if (double.IsNaN(HeightFraction) || HeightFraction < MinHeightFraction)
        {
            warnings.Add($"HeightFraction {HeightFraction} below minimum {MinHeightFraction}; clamped.");
            HeightFraction = MinHeightFraction;
        }
        else if (HeightFraction > MaxHeightFraction)
        {
            warnings.Add($"HeightFraction {HeightFraction} above maximum {MaxHeightFraction}; clamped.");
            HeightFraction = MaxHeightFraction;
        }

        return warnings;
    }

    public Config Clone() => (Config)MemberwiseClone();
}
