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

    public JumpscareService(IConfigService configService, ILoggingService logger, Func<bool>? shouldTrigger, ITimerFactory timerFactory)
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
        _logger.LogInfoAsync("jumpscare service started").GetAwaiter().GetResult();
    }

    public async Task ResumeAsync()
    {
        _config = await _configService.LoadAsync();
        lock (_lock)
        {
            _timer.Dispose();
            _state = JumpscareState.Idle;
            _timer = createTimer();
            startTimer();
        }
        _logger.LogInfoAsync("jumpscare service resumed").GetAwaiter().GetResult();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _timer.Dispose();
        }
        _logger.LogInfoAsync("jumpscare service stopped").GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    public JumpscareState CurrentState
    {
        get { lock (_lock) { return _state; } }
    }
    
    public void TriggerTick() => onTimerTick();

    private bool defaultShouldTrigger()
    {
        var rnd = new Random();
        return rnd.Next(1, _config.ChanceX + 1) == 1;
    }

    private void onTimerTick()
    {
        lock (_lock)
        {
            if (_state != JumpscareState.Idle)
                return;

            if (_shouldTrigger())
            {
                _state = JumpscareState.Playing;
                _logger.LogInfoAsync("jumpscare triggered").GetAwaiter().GetResult();
                JumpscareTriggered?.Invoke(this, EventArgs.Empty);

                _timer.Dispose();
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