using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Services.Abstractions;
using FoxyOverlay.Services.Enums;
using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;
using ITimerFactory = FoxyOverlay.Services.Utils.Abstractions.ITimerFactory;


namespace FoxyOverlay.Services;

/// <summary>
/// Rolls the dice once a second and drives playback when they come up Foxy.
/// </summary>
public sealed class JumpscareService : IHostedService, IDisposable
{
    /// <summary>How often the dice are rolled. One second, as in the Terraria mod.</summary>
    public static readonly TimeSpan RollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default upper bound on a single playback. A player that hangs — a wedged
    /// decoder, a window that never closes — must not take the whole service down with
    /// it, so playback is cancelled and the service returns to Idle regardless.
    /// </summary>
    public static readonly TimeSpan DefaultPlaybackTimeout = TimeSpan.FromSeconds(30);

    private readonly IConfigService _configService;
    private readonly ILoggingService _logger;
    private readonly IStatsService _stats;
    private readonly IJumpscarePlayer _player;
    private readonly ITimerFactory _timerFactory;
    private readonly Func<Config, bool> _shouldTrigger;
    private readonly object _lock = new object();

    private ITimer? _timer;
    private Config _config = new Config();
    private JumpscareState _state = JumpscareState.Stopped;
    private bool _disposed;

    public JumpscareService(
        IConfigService configService,
        ILoggingService logger,
        IStatsService stats,
        IJumpscarePlayer player,
        ITimerFactory timerFactory,
        Func<Config, bool>? shouldTrigger = null,
        TimeSpan? playbackTimeout = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
        _shouldTrigger = shouldTrigger ?? defaultShouldTrigger;
        PlaybackTimeout = playbackTimeout ?? DefaultPlaybackTimeout;
    }

    /// <summary>How long a single playback may take before it is cancelled.</summary>
    public TimeSpan PlaybackTimeout { get; }

    /// <summary>Raised after every state transition, for the tray tooltip.</summary>
    public event EventHandler<JumpscareState>? StateChanged;

    public JumpscareState CurrentState
    {
        get { lock (_lock) { return _state; } }
    }

    public Config CurrentConfig
    {
        get { lock (_lock) { return _config.Clone(); } }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Config config = await _configService.LoadAsync().ConfigureAwait(false);

        lock (_lock)
        {
            _config = config;
            _timer?.Dispose();
            _timer = _timerFactory.Create(_ => onTick());
            _timer.Change(RollInterval, RollInterval);
        }

        setState(config.Enabled ? JumpscareState.Idle : JumpscareState.Paused);
        await _logger.LogInfoAsync(
            $"jumpscare service started: 1-in-{config.ChanceX} per second, cooldown {config.CooldownSeconds}s, enabled={config.Enabled}")
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        setState(JumpscareState.Stopped);
        await _logger.LogInfoAsync("jumpscare service stopped").ConfigureAwait(false);
    }

    /// <summary>
    /// Re-reads config after the user saves settings. Unlike the old ResumeAsync this
    /// does not recreate the timer, so saving settings can never lose the roll loop.
    /// </summary>
    public async Task ReloadConfigAsync()
    {
        Config config = await _configService.LoadAsync().ConfigureAwait(false);

        lock (_lock)
        {
            _config = config;
            if (_state is JumpscareState.Idle or JumpscareState.Paused)
                _state = config.Enabled ? JumpscareState.Idle : JumpscareState.Paused;
        }

        StateChanged?.Invoke(this, CurrentState);
        await _logger.LogInfoAsync(
            $"config reloaded: 1-in-{config.ChanceX} per second, cooldown {config.CooldownSeconds}s, enabled={config.Enabled}")
            .ConfigureAwait(false);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_state != JumpscareState.Idle) return;
            _state = JumpscareState.Paused;
        }

        StateChanged?.Invoke(this, JumpscareState.Paused);
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_state != JumpscareState.Paused) return;
            _state = JumpscareState.Idle;
        }

        StateChanged?.Invoke(this, JumpscareState.Idle);
    }

    /// <summary>
    /// One roll of the dice, including playback and cooldown if it hits. Awaitable so
    /// tests are deterministic; the timer fires it and forgets.
    /// </summary>
    public async Task<bool> RollOnceAsync(CancellationToken cancellationToken = default)
    {
        Config config;
        lock (_lock)
        {
            if (_state != JumpscareState.Idle)
                return false;

            config = _config;
            if (!config.Enabled)
                return false;

            if (!_shouldTrigger(config))
            {
                _stats.RecordRoll();
                return false;
            }

            _state = JumpscareState.Playing;
        }

        _stats.RecordRoll();
        _stats.RecordJumpscare();
        StateChanged?.Invoke(this, JumpscareState.Playing);

        await playAsync(config, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Plays immediately regardless of the dice, for the tray's "Trigger now".</summary>
    public async Task TriggerNowAsync(CancellationToken cancellationToken = default)
    {
        Config config;
        lock (_lock)
        {
            if (_state is JumpscareState.Playing or JumpscareState.Cooldown)
                return;

            config = _config;
            _state = JumpscareState.Playing;
        }

        _stats.RecordJumpscare();
        StateChanged?.Invoke(this, JumpscareState.Playing);
        await _logger.LogInfoAsync("jumpscare triggered manually").ConfigureAwait(false);

        await playAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// The whole point of this method is that it always ends up back at Idle (or
    /// Paused): every failure mode is caught, the timeout bounds a hung player, and
    /// the cooldown runs in a finally.
    /// </summary>
    private async Task playAsync(Config config, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PlaybackTimeout);

            await _player.PlayAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _stats.RecordError();
            await _logger.LogErrorAsync($"playback exceeded {PlaybackTimeout.TotalSeconds:0}s and was cancelled")
                         .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _logger.LogInfoAsync("playback cancelled during shutdown").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _stats.RecordError();
            await _logger.LogErrorAsync($"playback failed: {ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
        }
        finally
        {
            setState(JumpscareState.Cooldown);

            if (config.CooldownSeconds > 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.CooldownSeconds), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            lock (_lock)
            {
                // Stopped wins: shutdown raced with playback and must not be undone.
                if (_state != JumpscareState.Stopped)
                    _state = _config.Enabled ? JumpscareState.Idle : JumpscareState.Paused;
            }

            StateChanged?.Invoke(this, CurrentState);
        }
    }

    private static bool defaultShouldTrigger(Config config) =>
        Random.Shared.Next(config.ChanceX) == 0;

    private async void onTick()
    {
        try
        {
            await RollOnceAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A timer callback throwing would tear down the process.
            try
            {
                _stats.RecordError();
                await _logger.LogErrorAsync($"roll failed: {ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private void setState(JumpscareState state)
    {
        lock (_lock)
        {
            if (_state == state) return;
            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }
}
