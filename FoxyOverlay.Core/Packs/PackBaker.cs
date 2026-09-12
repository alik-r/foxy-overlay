using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace FoxyOverlay.Core.Packs;

/// <summary>
/// Turns a green-screen video into a baked frame pack by shelling out to ffmpeg.
/// This is the C# twin of <c>scripts/bake.sh</c>, used when a user picks their own
/// video in the settings window. ffmpeg is an optional dependency: the bundled pack
/// is pre-baked, so the app runs fine without it.
/// </summary>
public sealed class PackBaker
{
    /// <summary>Default green-screen key, matching the bundled Foxy clip.</summary>
    public const string DefaultKeyColor = "0x00FE00";
    public const double DefaultSimilarity = 0.12;
    public const double DefaultBlend = 0.06;

    private readonly string _ffmpeg;
    private readonly string _ffprobe;

    public PackBaker(string ffmpeg = "ffmpeg", string ffprobe = "ffprobe")
    {
        _ffmpeg = ffmpeg;
        _ffprobe = ffprobe;
    }

    /// <summary>True when both ffmpeg and ffprobe can be launched.</summary>
    public bool IsAvailable
    {
        get
        {
            try
            {
                return probeVersion(_ffmpeg) && probeVersion(_ffprobe);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Where a baked copy of <paramref name="videoPath"/> lives in the per-user cache.
    /// Keyed by path and last-write time so editing the source re-bakes.
    /// </summary>
    public static string CacheDirectoryFor(string videoPath, string cacheRoot)
    {
        var stamp = File.Exists(videoPath)
            ? File.GetLastWriteTimeUtc(videoPath).Ticks.ToString(CultureInfo.InvariantCulture)
            : "0";
        var key = Path.GetFullPath(videoPath).ToLowerInvariant() + "|" + stamp;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return Path.Combine(cacheRoot, Path.GetFileNameWithoutExtension(videoPath) + "-" + hash);
    }

    public async Task<Pack> BakeAsync(
        string videoPath,
        string outputDirectory,
        string keyColor = DefaultKeyColor,
        double similarity = DefaultSimilarity,
        double blend = DefaultBlend,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoPath))
            throw new PackException($"video not found: {videoPath}");

        string width = await probeAsync(videoPath, "v:0", "stream=width", cancellationToken).ConfigureAwait(false);
        string height = await probeAsync(videoPath, "v:0", "stream=height", cancellationToken).ConfigureAwait(false);
        string rate = await probeAsync(videoPath, "v:0", "stream=r_frame_rate", cancellationToken).ConfigureAwait(false);
        string audioIndex = await probeAsync(videoPath, "a:0", "stream=index", cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, recursive: true);
        Directory.CreateDirectory(outputDirectory);

        // chromakey keys in YUV, which copes with compressed green edges better than
        // colorkey's RGB distance; despill then pulls the green cast out of pale
        // surfaces like teeth, which otherwise come through faintly olive.
        string filter = string.Format(
            CultureInfo.InvariantCulture,
            "chromakey={0}:{1:0.###}:{2:0.###},despill=type=green:mix=0.6:expand=0.4,format=rgba",
            keyColor, similarity, blend);

        await runAsync(_ffmpeg, new[]
        {
            "-v", "error", "-y", "-i", videoPath,
            "-vf", filter,
            "-start_number", "0",
            Path.Combine(outputDirectory, "frame_%03d.png")
        }, cancellationToken).ConfigureAwait(false);

        int frameCount = Directory.GetFiles(outputDirectory, "frame_*.png").Length;
        if (frameCount == 0)
            throw new PackException($"ffmpeg produced no frames from {videoPath}");

        string? audioFile = null;
        if (!string.IsNullOrWhiteSpace(audioIndex))
        {
            // SoundPlayer only accepts uncompressed PCM WAV.
            await runAsync(_ffmpeg, new[]
            {
                "-v", "error", "-y", "-i", videoPath,
                "-vn", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2",
                Path.Combine(outputDirectory, "audio.wav")
            }, cancellationToken).ConfigureAwait(false);
            audioFile = "audio.wav";
        }

        var manifest = new PackManifest
        {
            Name = Path.GetFileNameWithoutExtension(videoPath),
            Width = int.Parse(width, CultureInfo.InvariantCulture),
            Height = int.Parse(height, CultureInfo.InvariantCulture),
            FrameCount = frameCount,
            FrameRate = rate,
            FramePattern = "frame_{0:D3}.png",
            AudioFile = audioFile,
            Source = Path.GetFileName(videoPath),
            BakedWith = filter
        };

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, PackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

        return await new PackLoader().LoadAsync(outputDirectory, cancellationToken).ConfigureAwait(false);
    }

    private static bool probeVersion(string exe)
    {
        using var process = Process.Start(new ProcessStartInfo(exe, "-version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null) return false;
        process.WaitForExit(5000);
        return process.HasExited && process.ExitCode == 0;
    }

    private async Task<string> probeAsync(string videoPath, string stream, string entries, CancellationToken cancellationToken)
    {
        string output = await runAsync(_ffprobe, new[]
        {
            "-v", "error", "-select_streams", stream,
            "-show_entries", entries,
            "-of", "default=nw=1:nk=1", videoPath
        }, cancellationToken, allowFailure: true).ConfigureAwait(false);

        // Multi-stream inputs print one line per match; the first is the one selected.
        using var reader = new StringReader(output);
        return reader.ReadLine()?.Trim() ?? string.Empty;
    }

    private static async Task<string> runAsync(
        string exe,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure = false)
    {
        var info = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };

        try
        {
            if (!process.Start())
                throw new PackException($"could not start {exe}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
        {
            throw new PackException($"{exe} is not installed or not on PATH", ex);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);

        if (process.ExitCode != 0 && !allowFailure)
            throw new PackException($"{Path.GetFileName(exe)} exited with {process.ExitCode}: {error.Trim()}");

        return output;
    }
}
