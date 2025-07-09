using System;
using System.Windows;
using System.Windows.Resources;
using FoxyOverlay.Core;
using FoxyOverlay.Core.Extensions;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Media;
using FoxyOverlay.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

using Microsoft.Win32;


namespace FoxyOverlay.UI;

public partial class App : Application
{
    private IHost? _host;
    private ILoggingService? _logger;
    private JumpscareService? _jumpscareService;
    private IConfigService? _configService;
    private OverlayManager? _overlayManager;

    private WinForms.NotifyIcon? _notifyIcon;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FoxyOverlay";

    public static IServiceProvider ServiceProvider
        => ((App)Current)._host.Services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddConfigService();
                services.AddLoggingService();
                services.AddSingleton<JumpscareService>();
                services.AddSingleton<OverlayManager>();
            })
            .Build();
        
        _host.StartAsync().GetAwaiter().GetResult();
        
        _configService = _host.Services.GetRequiredService<IConfigService>();
        _logger = _host.Services.GetRequiredService<ILoggingService>();
        _jumpscareService = _host.Services.GetRequiredService<JumpscareService>();
        _overlayManager = _host.Services.GetRequiredService<OverlayManager>();

        _jumpscareService.JumpscareTriggered += async (s, args) =>
        {
            try
            {
                Config config = await _configService.LoadAsync();
                await _overlayManager.PlayAsync(config.VideoPath, config.IsMuted);
                await _jumpscareService.ResumeAsync();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"error in jumpscare handler: {ex}");
            }
        };
        
        _jumpscareService.StartAsync(default).GetAwaiter().GetResult();
        
        SetupNotifyIcon();
        
        TryRegisterInStartup();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.LogInfoAsync("UI exiting").GetAwaiter().GetResult();
        
        TryUnregisterFromStartup();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
        
        base.OnExit(e);
}

    private void TryUnregisterFromStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            _logger.LogErrorAsync($"failed to unregister from startup {ex.Message}").GetAwaiter().GetResult();
        }
    }

    private void TryRegisterInStartup()
    {
        try
        {
            string exe = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.SetValue(RunValueName, $"\"{exe}\"");
        }
        catch (Exception ex)
        {
            _logger.LogErrorAsync($"failed to register to startup {ex.Message}").GetAwaiter().GetResult();
        } 
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Visible = true
        };

        // load embedded icon via pack URI
        var uri = new Uri("pack://application:,,,/Assets/foxy.ico");
        StreamResourceInfo? sri = GetResourceStream(uri);
        if (sri?.Stream != null)
            _notifyIcon.Icon = new Drawing.Icon(sri.Stream);
        

        // build context menu
        var menu = new WinForms.ContextMenuStrip();

        var miSettings = new WinForms.ToolStripMenuItem("Open Settings");
        miSettings.Click += (s, a) => OnOpenSettings();

        var miRestart  = new WinForms.ToolStripMenuItem("Restart Service");
        miRestart.Click += (s, a) => OnRestartServiceAsync();

        var miQuit     = new WinForms.ToolStripMenuItem("Quit");
        miQuit.Click += (s, a) => OnQuit();

        menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            miSettings,
            miRestart,
            new WinForms.ToolStripSeparator(),
            miQuit
        });

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void OnOpenSettings()
    {
        SettingsWindow wnd = new SettingsWindow
        {
            Owner = Application.Current.MainWindow
        };
        wnd.ShowDialog();
    }

    private async void OnRestartServiceAsync()
    {
        try
        {
            await _jumpscareService.ResumeAsync();
            await _logger.LogInfoAsync("jumpscare service manually restarted");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"error restarting service: {ex}");
        }
    }

    private void OnQuit()
    {
        _host.StopAsync().GetAwaiter().GetResult(); Shutdown();
    }
}

