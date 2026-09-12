using System;
using System.Globalization;
using System.Text.Json.Serialization;


namespace FoxyOverlay.Core.Packs;

/// <summary>
/// The contents of a pack's <c>pack.json</c>, as written by <c>scripts/bake.sh</c>.
/// </summary>
public sealed class PackManifest
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameCount { get; set; }

    /// <summary>Frame rate as an exact rational, e.g. "24000/1001". A bare number is also accepted.</summary>
    public string FrameRate { get; set; } = "24/1";

    /// <summary>Composite format for frame file names, e.g. "frame_{0:D3}.png".</summary>
    public string FramePattern { get; set; } = "frame_{0:D3}.png";

    /// <summary>PCM WAV file next to the frames, or null for a silent pack.</summary>
    public string? AudioFile { get; set; }

    public string? Source { get; set; }
    public string? BakedWith { get; set; }

    [JsonIgnore]
    public double Fps => ParseFrameRate(FrameRate);

    [JsonIgnore]
    public TimeSpan FrameInterval => TimeSpan.FromSeconds(1.0 / Fps);

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(FrameCount / Fps);

    /// <summary>Parses "24000/1001" or "23.976". Throws <see cref="FormatException"/> on anything else.</summary>
    public static double ParseFrameRate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("frame rate is empty");

        int slash = value.IndexOf('/');
        if (slash < 0)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain) && plain > 0)
                return plain;
            throw new FormatException($"'{value}' is not a valid frame rate");
        }

        if (double.TryParse(value.AsSpan(0, slash), NumberStyles.Float, CultureInfo.InvariantCulture, out double num) &&
            double.TryParse(value.AsSpan(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double den) &&
            num > 0 && den > 0)
        {
            return num / den;
        }

        throw new FormatException($"'{value}' is not a valid frame rate");
    }
}
