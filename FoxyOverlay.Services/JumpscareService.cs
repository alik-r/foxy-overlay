using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Services.Enums;
using FoxyOverlay.Services.Utils.Abstractions;
using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services;

public class JumpscareService : IHostedService, IDisposable
{
    private readonly IConfigService _configService;
    private readonly ILoggingService _logger;
    private readonly Func<bool> _shouldTrigger;
    private readonly object _lock = new object();
    private readonly ITimerFactory _timerFactory;
    private ITimer _timer = null!;
    private Config _config = null!;
    private JumpscareState _state = JumpscareState.Idle;

    public event EventHandler? JumpscareTriggered;

    public JumpscareService(IConfigService configService, ILoggingService logger, ITimerFactory timerFactory, Func<bool>? shouldTrigger = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _shouldTrigger = shouldTrigger ?? defaultShouldTrigger;
        _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
    }

    public void Dispose() => _timer?.Dispose();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _config = await _configService.LoadAsync();
        lock (_lock)
        {
            _state = JumpscareState.Idle;
            _timer = createTimer();
            startTimer();
        }

        await _logger.LogInfoAsync("jumpscare service started");
    }

    public async Task ResumeAsync()
    {
        _config = await _configService.LoadAsync();
        lock (_lock)
        {
            _timer?.Dispose();
            _state = JumpscareState.Idle;
            _timer = createTimer();
            startTimer();
        }

        await _logger.LogInfoAsync("jumpscare service resumed");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _timer.Dispose();
        }
        await _logger.LogInfoAsync("jumpscare service stopped");
    }

    public JumpscareState CurrentState
    {
        get { lock (_lock) { return _state; } }
    }
    
    public void TriggerTick() => onTimerTick();

    private bool defaultShouldTrigger()
    {
        return Random.Shared.Next(1, _config.ChanceX + 1) == 1;
    }

    private async void onTimerTick()
    {
        bool trigger = false;
        lock (_lock)
        {
            if (_state != JumpscareState.Idle)
                return;

            if (_shouldTrigger())
            {
                _state = JumpscareState.Playing;
                trigger = true;
            }
        }

        if (trigger)
        {
            try
            {
                await _logger.LogInfoAsync("jumpscare triggered");
                JumpscareTriggered?.Invoke(this, EventArgs.Empty);
                lock (_lock)
                {
                    _timer?.Dispose();
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error during jumpscare trigger: {ex.Message}");
                lock (_lock)
                {
                    _state = JumpscareState.Idle;
                }
            }
        }
    }

    private ITimer createTimer()
    {
        return _timerFactory.Create(_ => onTimerTick());
    }

    private void startTimer()
    {
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
}