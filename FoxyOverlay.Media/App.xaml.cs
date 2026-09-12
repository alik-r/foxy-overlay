using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

using Microsoft.Extensions.DependencyInjection;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Extensions;
using FoxyOverlay.Core.Packs;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Media.Overlay;
using FoxyOverlay.Services;
using FoxyOverlay.Services.Abstractions;
using FoxyOverlay.Services.Enums;
using FoxyOverlay.Services.Extensions;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;


namespace FoxyOverlay.Media;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\FoxyOverlay.SingleInstance";

    /// <summary>Stats are counted in memory and written out on this cadence.</summary>
    private static readonly TimeSpan StatsFlushInterval = TimeSpan.FromMinutes(1);

    private Mutex? _singleInstance;
    private ServiceProvider? _services;
    private NotifyIcon? _trayIcon;
    private JumpscareService? _jumpscareService;
    private OverlayPlayer? _player;
    private ILoggingService? _logger;
    private IStatsService? _stats;
    private System.Threading.Timer? _statsTimer;
    private SettingsWindow? _settingsWindow;
    private ToolStripMenuItem? _pauseItem;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Foxy Overlay is already running — look in the notification area.",
                "Foxy Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += onDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += onDomainException;

        try
        {
            AppPaths.EnsureDataDirectory();

            var services = new ServiceCollection();
            services.AddFoxyCore();
            services.AddFoxyServices();
            services.AddSingleton<IJumpscarePlayer>(provider => new OverlayPlayer(
                provider.GetRequiredService<IConfigService>(),
                provider.GetRequiredService<ILoggingService>(),
                provider.GetRequiredService<PackLoader>(),
                provider.GetRequiredService<PackBaker>(),
                Dispatcher));

            _services = services.BuildServiceProvider();
            _logger = _services.GetRequiredService<ILoggingService>();
            _stats = _services.GetRequiredService<IStatsService>();
            _jumpscareService = _services.GetRequiredService<JumpscareService>();
            _player = (OverlayPlayer)_services.GetRequiredService<IJumpscarePlayer>();

            await _logger.LogInfoAsync($"foxy overlay starting (pid {Environment.ProcessId})");

            await _stats.LoadAsync();
            buildTrayIcon();

            // Decode frames and build the overlay windows before the dice start
            // rolling, so the very first scare is as prompt as every later one.
            try
            {
                await _player.PreloadAsync();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"pack preload failed: {ex.Message}");
                showTrayWarning("Foxy Overlay could not load its video pack. See the log for details.");
            }

            _jumpscareService.StateChanged += onStateChanged;
            await _jumpscareService.StartAsync(CancellationToken.None);

            _statsTimer = new System.Threading.Timer(
                _ => _ = flushStatsAsync(), null, StatsFlushInterval, StatsFlushInterval);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Foxy Overlay failed to start:{Environment.NewLine}{Environment.NewLine}{ex}",
                "Foxy Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_jumpscareService != null)
                await _jumpscareService.StopAsync(CancellationToken.None);

            if (_statsTimer != null)
                await _statsTimer.DisposeAsync();

            if (_stats != null)
                await _stats.FlushAsync();

            if (_logger != null)
                await _logger.LogInfoAsync("foxy overlay stopped");
        }
        catch (Exception)
        {
            // Shutdown is best-effort; never hang the process on the way out.
        }
        finally
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            _player?.Dispose();
            _services?.Dispose();
            _singleInstance?.Dispose();
        }

        base.OnExit(e);
    }

    private void buildTrayIcon()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => showSettings()));
        menu.Items.Add(new ToolStripMenuItem("Trigger now", null, (_, _) => _ = triggerNowAsync()));

        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => togglePause());
        menu.Items.Add(_pauseItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Shutdown()));

        _trayIcon = new NotifyIcon
        {
            Icon = loadTrayIcon(),
            Text = "Foxy Overlay",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => showSettings();
    }

    private static Icon loadTrayIcon()
    {
        try
        {
            var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/foxy.ico"));
            if (resource != null)
            {
                using var stream = resource.Stream;
                return new Icon(stream);
            }
        }
        catch (Exception)
        {
            // Fall through to a stock icon rather than failing to show a tray entry at all.
        }

        return SystemIcons.Application;
    }

    private void showSettings()
    {
        if (_services == null || _jumpscareService == null || _player == null)
            return;

        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(
            _services.GetRequiredService<IConfigService>(),
            _services.GetRequiredService<IStatsService>(),
            _services.GetRequiredService<ILoggingService>(),
            _jumpscareService,
            _services.GetRequiredService<PackBaker>(),
            onSettingsSaved);

        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Re-reads config and re-decodes the pack if the user changed it.</summary>
    private async Task onSettingsSaved()
    {
        if (_jumpscareService == null || _player == null) return;

        await _jumpscareService.ReloadConfigAsync();
        await _player.InvalidateAsync();
        await _player.PreloadAsync();
        updateTrayText(_jumpscareService.CurrentState);
    }

    private async Task triggerNowAsync()
    {
        if (_jumpscareService == null) return;

        try
        {
            await _jumpscareService.TriggerNowAsync();
        }
        catch (Exception ex)
        {
            if (_logger != null)
                await _logger.LogErrorAsync($"manual trigger failed: {ex.Message}");
        }
    }

    private void togglePause()
    {
        if (_jumpscareService == null) return;

        if (_jumpscareService.CurrentState == JumpscareState.Paused)
            _jumpscareService.Resume();
        else
            _jumpscareService.Pause();
    }

    private void onStateChanged(object? sender, JumpscareState state) =>
        Dispatcher.InvokeAsync(() => updateTrayText(state));

    private void updateTrayText(JumpscareState state)
    {
        if (_trayIcon == null || _jumpscareService == null) return;

        if (_pauseItem != null)
            _pauseItem.Text = state == JumpscareState.Paused ? "Resume" : "Pause";

        Config config = _jumpscareService.CurrentConfig;
        string status = state switch
        {
            JumpscareState.Paused => "paused",
            JumpscareState.Playing => "playing",
            JumpscareState.Cooldown => "cooling down",
            JumpscareState.Stopped => "stopped",
            _ => $"1 in {config.ChanceX:N0} per second"
        };

        // NotifyIcon.Text throws above 63 characters on some Windows versions.
        string text = $"Foxy Overlay - {status}";
        _trayIcon.Text = text.Length > 62 ? text[..62] : text;
    }

    private void showTrayWarning(string message)
    {
        Dispatcher.InvokeAsync(() =>
            _trayIcon?.ShowBalloonTip(8000, "Foxy Overlay", message, ToolTipIcon.Warning));
    }

    private async Task flushStatsAsync()
    {
        try
        {
            if (_stats != null)
                await _stats.FlushAsync();
        }
        catch (Exception)
        {
        }
    }

    private async void onDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // A tray app that vanishes on an unhandled UI exception is indistinguishable
        // from one that crashed silently. Log it and carry on.
        e.Handled = true;
        _stats?.RecordError();

        if (_logger != null)
            await _logger.LogErrorAsync($"unhandled UI exception: {e.Exception}");
    }

    private void onDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            _logger?.LogErrorAsync($"unhandled exception: {e.ExceptionObject}").GetAwaiter().GetResult();
            _stats?.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
        }
    }
}
