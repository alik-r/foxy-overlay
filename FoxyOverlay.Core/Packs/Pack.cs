using System.Collections.Generic;


namespace FoxyOverlay.Core.Packs;

/// <summary>
/// A validated pack: a manifest plus resolved absolute paths to every file it needs.
/// Holding no decoded pixels keeps this type usable from non-Windows unit tests.
/// </summary>
public sealed class Pack
{
    public Pack(string directory, PackManifest manifest, IReadOnlyList<string> framePaths, string? audioPath)
    {
        Directory = directory;
        Manifest = manifest;
        FramePaths = framePaths;
        AudioPath = audioPath;
    }

    public string Directory { get; }
    public PackManifest Manifest { get; }

    /// <summary>Absolute frame paths in playback order.</summary>
    public IReadOnlyList<string> FramePaths { get; }

    /// <summary>Absolute path to the PCM WAV track, or null for a silent pack.</summary>
    public string? AudioPath { get; }

    public bool HasAudio => AudioPath != null;
}
