using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Packs;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Services.Abstractions;


namespace FoxyOverlay.Media.Overlay;

/// <summary>
/// Puts Foxy on every screen and returns when the clip has finished.
/// </summary>
public sealed class OverlayPlayer : IJumpscarePlayer, IDisposable
{
    private readonly IConfigService _configService;
    private readonly ILoggingService _logger;
    private readonly PackLoader _packLoader;
    private readonly PackBaker _packBaker;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _packLock = new SemaphoreSlim(1, 1);

    private FramePack? _pack;
    private Config? _cachedConfig;
    private string? _packKey;
    private WriteableBitmap? _surface;
    private readonly List<OverlayWindow> _windows = new List<OverlayWindow>();
    private IReadOnlyList<System.Drawing.Rectangle> _windowBounds = Array.Empty<System.Drawing.Rectangle>();
    private bool _disposed;
    private bool _diagnoseBounds = true;

    public OverlayPlayer(
        IConfigService configService,
        ILoggingService logger,
        PackLoader packLoader,
        PackBaker packBaker,
        Dispatcher dispatcher)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _packLoader = packLoader ?? throw new ArgumentNullException(nameof(packLoader));
        _packBaker = packBaker ?? throw new ArgumentNullException(nameof(packBaker));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The pack currently held in memory, or null if none has loaded yet.</summary>
    public FramePack? LoadedPack => _pack;

    /// <summary>
    /// Decodes the configured pack ahead of time. Called at startup and after the user
    /// changes packs, so that a trigger never waits on disk.
    /// </summary>
    public async Task PreloadAsync(CancellationToken cancellationToken = default)
    {
        Config config = await _configService.LoadAsync().ConfigureAwait(false);
        _cachedConfig = config;
        FramePack pack = await ensurePackAsync(config, cancellationToken).ConfigureAwait(false);

        // Build the overlay windows now. Constructing a layered WPF window costs a few
        // hundred milliseconds, which was the bulk of the delay between the trigger and
        // Foxy appearing; created up front and reused, showing one is near-instant.
        await _dispatcher.InvokeAsync(() => ensureWindows(pack, config)).Task.ConfigureAwait(false);
    }

    public async Task PlayAsync(CancellationToken cancellationToken)
    {
        // Reading config from disk here would put file I/O between the trigger and the
        // first frame. The cache is refreshed by PreloadAsync whenever settings change.
        Config config = _cachedConfig ??= await _configService.LoadAsync().ConfigureAwait(false);
        FramePack pack = await ensurePackAsync(config, cancellationToken).ConfigureAwait(false);

        var timing = new PlaybackTiming();
        await playOnUiThreadAsync(pack, config, timing, cancellationToken).ConfigureAwait(false);

        await _logger.LogInfoAsync(
            $"jumpscare finished: {pack.Frames.Count} frames, " +
            $"first frame on screen in {timing.TimeToFirstFrame.TotalMilliseconds:0}ms, " +
            $"clip {timing.ClipTime.TotalMilliseconds:0}ms (nominal {pack.Manifest.Duration.TotalMilliseconds:0}ms), " +
            $"teardown {timing.Teardown.TotalMilliseconds:0}ms, " +
            $"{timing.FramesShown}/{pack.Frames.Count} frames drawn")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Where the wall-clock goes during one jumpscare. Time-to-first-frame is the
    /// number that matters: it is the gap between the dice coming up Foxy and the
    /// user actually seeing anything.
    /// </summary>
    private sealed class PlaybackTiming
    {
        public TimeSpan TimeToFirstFrame { get; set; }
        public TimeSpan ClipTime { get; set; }
        public TimeSpan Teardown { get; set; }
        public int FramesShown { get; set; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_dispatcher.CheckAccess())
            destroyWindows();
        else
            _dispatcher.Invoke(destroyWindows);

        _pack?.Dispose();
        _pack = null;
        _packLock.Dispose();
    }

    /// <summary>Drops the cached pack so the next play re-reads it from disk.</summary>
    public async Task InvalidateAsync()
    {
        await _packLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _pack?.Dispose();
            _pack = null;
            _packKey = null;
            _surface = null;
            _cachedConfig = null;

            // The pool holds the old surface and is sized for the old config.
            await _dispatcher.InvokeAsync(destroyWindows).Task.ConfigureAwait(false);
        }
        finally
        {
            _packLock.Release();
        }
    }

    private async Task<FramePack> ensurePackAsync(Config config, CancellationToken cancellationToken)
    {
        // Muting changes whether audio is loaded at all, so it is part of the identity.
        string key = $"{config.PackPath ?? "<bundled>"}|muted={config.IsMuted}";

        await _packLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_pack != null && _packKey == key)
                return _pack;

            Pack pack = await resolvePackAsync(config, cancellationToken).ConfigureAwait(false);
            FramePack loaded = await FramePack.LoadAsync(pack, config.IsMuted, cancellationToken).ConfigureAwait(false);

            _pack?.Dispose();
            _pack = loaded;
            _packKey = key;

            await _logger.LogInfoAsync(
                $"pack '{loaded.Manifest.Name}' loaded from {pack.Directory}: " +
                $"{loaded.Frames.Count} frames at {loaded.PixelWidth}x{loaded.PixelHeight}, " +
                $"{loaded.Manifest.Fps:0.##}fps, {loaded.ApproximateBytes / (1024 * 1024)}MB of pixels, " +
                $"decoded in {loaded.LoadTime.TotalMilliseconds:0}ms; " +
                $"process working set {Environment.WorkingSet / (1024 * 1024)}MB")
                .ConfigureAwait(false);

            return loaded;
        }
        finally
        {
            _packLock.Release();
        }
    }

    /// <summary>
    /// Turns whatever the user pointed at — nothing, a pack directory, or a raw video —
    /// into a loadable pack, baking through ffmpeg when necessary.
    /// </summary>
    private async Task<Pack> resolvePackAsync(Config config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.PackPath))
            return await _packLoader.LoadAsync(PackLoader.BundledPackDirectory, cancellationToken).ConfigureAwait(false);

        string path = Path.GetFullPath(config.PackPath);

        if (Directory.Exists(path))
            return await _packLoader.LoadAsync(path, cancellationToken).ConfigureAwait(false);

        // A pack.json was picked directly; use its directory.
        if (File.Exists(path) && Path.GetFileName(path).Equals(PackLoader.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            return await _packLoader.LoadAsync(Path.GetDirectoryName(path)!, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(path))
        {
            await _logger.LogWarnAsync($"configured pack '{path}' not found; falling back to the bundled pack")
                         .ConfigureAwait(false);
            return await _packLoader.LoadAsync(PackLoader.BundledPackDirectory, cancellationToken).ConfigureAwait(false);
        }

        // A video file: bake it, reusing the cache when the source has not changed.
        string cacheDirectory = PackBaker.CacheDirectoryFor(path, AppPaths.PackCacheDirectory);
        if (PackLoader.IsPackDirectory(cacheDirectory))
            return await _packLoader.LoadAsync(cacheDirectory, cancellationToken).ConfigureAwait(false);

        await _logger.LogInfoAsync($"baking '{path}' into {cacheDirectory}").ConfigureAwait(false);
        return await _packBaker.BakeAsync(path, cacheDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Task playOnUiThreadAsync(
        FramePack pack, Config config, PlaybackTiming timing, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var total = Stopwatch.StartNew();

        _dispatcher.InvokeAsync(() =>
        {
            var windows = new List<OverlayWindow>();
            EventHandler? onRendering = null;
            CancellationTokenRegistration registration = default;
            var clock = new Stopwatch();
            int shownIndex = -1;
            int framesShown = 0;
            bool finished = false;

            void finish()
            {
                if (finished) return;
                finished = true;

                timing.ClipTime = clock.Elapsed;
                timing.FramesShown = framesShown;
                var teardown = Stopwatch.StartNew();

                if (onRendering != null)
                    CompositionTarget.Rendering -= onRendering;
                registration.Dispose();
                clock.Stop();

                try
                {
                    pack.Audio?.Stop();
                }
                catch (Exception)
                {
                    // Stopping a sound that already finished is not worth failing over.
                }

                foreach (OverlayWindow window in windows)
                {
                    try
                    {
                        window.HideOverlay();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                windows.Clear();
                timing.Teardown = teardown.Elapsed;
                completion.TrySetResult();
            }

            try
            {
                windows.AddRange(ensureWindows(pack, config));

                // Write frame 0 before anything becomes visible, so there is never a
                // blank flash while the render loop spins up.
                writeFrame(pack, 0);
                shownIndex = 0;
                framesShown = 1;

                foreach (OverlayWindow window in windows)
                    window.ShowOverlay();

                if (_diagnoseBounds)
                {
                    _diagnoseBounds = false;
                    foreach (OverlayWindow window in windows)
                        _ = _logger.LogInfoAsync("overlay bounds: " + window.DescribeBounds());
                }

                if (windows.Count == 0)
                {
                    finish();
                    return;
                }

                timing.TimeToFirstFrame = total.Elapsed;

                pack.Audio?.Play();
                clock.Start();

                long interval = Math.Max(1, pack.FrameInterval.Ticks);

                onRendering = (_, _) =>
                {
                    // Index by wall-clock rather than counting renders: if the machine is
                    // busy the clip drops frames and still ends on time, staying in sync
                    // with the audio instead of drifting behind it.
                    int index = (int)(clock.Elapsed.Ticks / interval);

                    if (index >= pack.Frames.Count)
                    {
                        finish();
                        return;
                    }

                    if (index == shownIndex)
                        return;

                    shownIndex = index;
                    framesShown++;
                    writeFrame(pack, index);
                };

                CompositionTarget.Rendering += onRendering;
                registration = cancellationToken.Register(() => _dispatcher.InvokeAsync(finish));
            }
            catch (Exception ex)
            {
                finish();
                completion.TrySetException(ex);
            }
        }, DispatcherPriority.Send);

        return completion.Task;
    }

    /// <summary>
    /// Returns the overlay windows for the current screen layout, building them the
    /// first time and rebuilding only if the screens or the configured size changed.
    /// Must be called on the UI thread.
    /// </summary>
    private IReadOnlyList<OverlayWindow> ensureWindows(FramePack pack, Config config)
    {
        var desired = new List<System.Drawing.Rectangle>();
        foreach (Screen screen in targetScreens(config))
            desired.Add(overlayBounds(screen, pack, config));

        bool matches = _windows.Count == desired.Count && _windowBounds.Count == desired.Count;
        if (matches)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                if (_windowBounds[i] != desired[i])
                {
                    matches = false;
                    break;
                }
            }
        }

        if (matches)
            return _windows;

        destroyWindows();

        WriteableBitmap surface = ensureSurface(pack);
        foreach (System.Drawing.Rectangle bounds in desired)
        {
            var window = new OverlayWindow();
            window.SetSurface(surface);
            window.SetPhysicalBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            // Realise the HWND now so the first ShowOverlay() has nothing left to build.
            window.PrepareHidden();
            window.SetPhysicalBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            _windows.Add(window);
        }

        _windowBounds = desired;
        return _windows;
    }

    /// <summary>Closes the pooled windows. Must be called on the UI thread.</summary>
    private void destroyWindows()
    {
        foreach (OverlayWindow window in _windows)
        {
            try
            {
                window.CloseOverlay();
            }
            catch (InvalidOperationException)
            {
            }
        }

        _windows.Clear();
        _windowBounds = Array.Empty<System.Drawing.Rectangle>();
    }

    /// <summary>
    /// The single surface every overlay window draws. Created once per pack and reused
    /// across jumpscares, so repeated scares allocate nothing.
    /// </summary>
    private WriteableBitmap ensureSurface(FramePack pack)
    {
        if (_surface != null &&
            _surface.PixelWidth == pack.PixelWidth &&
            _surface.PixelHeight == pack.PixelHeight)
        {
            return _surface;
        }

        _surface = new WriteableBitmap(
            pack.PixelWidth, pack.PixelHeight, 96, 96, FramePack.Format, null);
        return _surface;
    }

    private void writeFrame(FramePack pack, int index)
    {
        WriteableBitmap surface = ensureSurface(pack);
        surface.WritePixels(
            new Int32Rect(0, 0, pack.PixelWidth, pack.PixelHeight),
            (byte[])pack.Frames[index],
            pack.Stride,
            0);
    }

    private static IEnumerable<Screen> targetScreens(Config config) =>
        config.AllMonitors ? Screen.AllScreens : new[] { Screen.PrimaryScreen ?? Screen.AllScreens[0] };

    /// <summary>Maps a screen and the configured size onto concrete window bounds.</summary>
    private static System.Drawing.Rectangle overlayBounds(Screen screen, FramePack pack, Config config)
    {
        var bounds = screen.Bounds;
        OverlayRect rect = OverlayGeometry.Fit(
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            pack.PixelWidth, pack.PixelHeight,
            config.HeightFraction);

        return new System.Drawing.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
