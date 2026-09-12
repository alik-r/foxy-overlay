using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using FoxyOverlay.Core.Packs;


namespace FoxyOverlay.Media.Overlay;

/// <summary>
/// A pack decoded into raw BGRA pixel buffers, ready to blit into a WriteableBitmap.
///
/// Deliberately NOT a list of BitmapSource. WPF keeps a render-side copy of every
/// distinct ImageSource it draws, so 25 frames as 25 bitmaps cost several hundred
/// megabytes of surfaces for a one-second clip. Plain byte[] plus a single shared
/// WriteableBitmap costs the pixels once.
///
/// Decoding happens once, up front, rather than at trigger time: a jumpscare that
/// arrives a third of a second late is a much worse jumpscare.
/// </summary>
public sealed class FramePack : IDisposable
{
    /// <summary>What the frames are decoded to, and what the overlay surface must be.</summary>
    public static readonly PixelFormat Format = PixelFormats.Pbgra32;

    private FramePack(
        Pack pack, IReadOnlyList<byte[]> frames, SoundPlayer? audio,
        int pixelWidth, int pixelHeight, int stride, TimeSpan loadTime)
    {
        Source = pack;
        Frames = frames;
        Audio = audio;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Stride = stride;
        LoadTime = loadTime;
    }

    public Pack Source { get; }

    /// <summary>Frame pixel buffers in playback order, each <see cref="Stride"/> * height bytes.</summary>
    public IReadOnlyList<byte[]> Frames { get; }

    public SoundPlayer? Audio { get; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public int Stride { get; }
    public TimeSpan LoadTime { get; }

    public PackManifest Manifest => Source.Manifest;
    public TimeSpan FrameInterval => Manifest.FrameInterval;
    public long ApproximateBytes => (long)Stride * PixelHeight * Frames.Count;

    /// <summary>Decodes every frame. Safe to call from a background thread.</summary>
    public static Task<FramePack> LoadAsync(Pack pack, bool muted, CancellationToken cancellationToken = default)
    {
        if (pack == null) throw new ArgumentNullException(nameof(pack));

        return Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();

            int width = 0, height = 0, stride = 0;
            var frames = new List<byte[]>(pack.FramePaths.Count);

            foreach (string path in pack.FramePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BitmapSource decoded = decode(path);

                if (width == 0)
                {
                    width = decoded.PixelWidth;
                    height = decoded.PixelHeight;
                    stride = width * ((Format.BitsPerPixel + 7) / 8);
                }
                else if (decoded.PixelWidth != width || decoded.PixelHeight != height)
                {
                    throw new PackException(
                        $"frame {Path.GetFileName(path)} is {decoded.PixelWidth}x{decoded.PixelHeight}, " +
                        $"but the pack's frames are {width}x{height}");
                }

                var buffer = new byte[stride * height];
                decoded.CopyPixels(buffer, stride, 0);
                frames.Add(buffer);
            }

            if (frames.Count == 0)
                throw new PackException($"pack {pack.Manifest.Name} decoded to zero frames");

            SoundPlayer? audio = null;
            if (!muted && pack.AudioPath is { } audioPath)
            {
                try
                {
                    audio = new SoundPlayer(audioPath);
                    // Preload so the first Play() does not block on disk.
                    audio.Load();
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    audio?.Dispose();
                    audio = null;
                }
            }

            stopwatch.Stop();
            return new FramePack(pack, frames, audio, width, height, stride, stopwatch.Elapsed);
        }, cancellationToken);
    }

    private static BitmapSource decode(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // OnLoad so nothing keeps a handle on the user's pack directory afterwards.
        var decoder = BitmapDecoder.Create(
            stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

        BitmapSource frame = decoder.Frames[0];

        // Premultiplied BGRA is what a WriteableBitmap surface wants; converting here
        // means no per-frame conversion during playback.
        return frame.Format == Format ? frame : new FormatConvertedBitmap(frame, Format, null, 0);
    }

    public void Dispose() => Audio?.Dispose();
}
