using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace FoxyOverlay.Core.Packs;

/// <summary>
/// Reads and validates baked frame packs from disk.
/// </summary>
public sealed class PackLoader
{
    public const string ManifestFileName = "pack.json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Directory of the pack shipped alongside the executable.</summary>
    public static string BundledPackDirectory =>
        Path.Combine(AppContext.BaseDirectory, "assets", "foxy");

    /// <summary>
    /// Picks the pack directory a config refers to, falling back to the bundled pack
    /// when <see cref="Config.PackPath"/> is unset.
    /// </summary>
    public static string ResolveDirectory(Config config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        return string.IsNullOrWhiteSpace(config.PackPath)
            ? BundledPackDirectory
            : Path.GetFullPath(config.PackPath);
    }

    /// <summary>True if the directory looks like a pack, i.e. it contains a manifest.</summary>
    public static bool IsPackDirectory(string directory) =>
        !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, ManifestFileName));

    /// <summary>
    /// Loads and fully validates a pack. Every frame is checked for existence here so
    /// that a broken pack fails at startup rather than midway through a jumpscare.
    /// </summary>
    public async Task<Pack> LoadAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new PackException("pack directory was not specified");

        directory = Path.GetFullPath(directory);

        if (!Directory.Exists(directory))
            throw new PackException($"pack directory does not exist: {directory}");

        string manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new PackException($"pack is missing {ManifestFileName}: {directory}");

        PackManifest manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<PackManifest>(stream, JsonOptions, cancellationToken)
                              .ConfigureAwait(false)
                       ?? throw new PackException($"{manifestPath} deserialized to null");
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            throw new PackException($"could not read {manifestPath}: {ex.Message}", ex);
        }

        validate(manifest, manifestPath);

        var framePaths = new List<string>(manifest.FrameCount);
        for (int i = 0; i < manifest.FrameCount; i++)
        {
            string name = string.Format(CultureInfo.InvariantCulture, manifest.FramePattern, i);
            string path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new PackException($"pack {manifest.Name} declares {manifest.FrameCount} frames but {name} is missing");
            framePaths.Add(path);
        }

        string? audioPath = null;
        if (!string.IsNullOrWhiteSpace(manifest.AudioFile))
        {
            audioPath = Path.Combine(directory, manifest.AudioFile);
            if (!File.Exists(audioPath))
                throw new PackException($"pack {manifest.Name} declares audio {manifest.AudioFile} but the file is missing");
        }

        return new Pack(directory, manifest, framePaths, audioPath);
    }

    private static void validate(PackManifest manifest, string manifestPath)
    {
        if (manifest.FrameCount <= 0)
            throw new PackException($"{manifestPath}: frameCount must be positive, got {manifest.FrameCount}");
        if (manifest.Width <= 0 || manifest.Height <= 0)
            throw new PackException($"{manifestPath}: width and height must be positive, got {manifest.Width}x{manifest.Height}");
        if (string.IsNullOrWhiteSpace(manifest.FramePattern) || !manifest.FramePattern.Contains("{0"))
            throw new PackException($"{manifestPath}: framePattern must contain a '{{0}}' placeholder, got '{manifest.FramePattern}'");

        try
        {
            if (manifest.Fps <= 0)
                throw new PackException($"{manifestPath}: frameRate must be positive, got '{manifest.FrameRate}'");
        }
        catch (FormatException ex)
        {
            throw new PackException($"{manifestPath}: {ex.Message}", ex);
        }
    }
}
