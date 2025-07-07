using System;
using System.Windows;
using System.Windows.Resources;
using FoxyOverlay.Core.Extensions;
using FoxyOverlay.Core.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

using Microsoft.Win32;


namespace FoxyOverlay.UI;

public partial class App : Application
{
    private ILoggingService _logger;

    private WinForms.NotifyIcon? _notifyIcon;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FoxyOverlay";

    public static IServiceProvider ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection()
            .AddLoggingService()
            .AddConfigService()
            .AddTransient<SettingsViewModel>();
        
        ServiceProvider = services.BuildServiceProvider();
        
        _logger = ServiceProvider.GetRequiredService<ILoggingService>();
        _logger.LogInfoAsync("UI starting").GetAwaiter().GetResult();
        
        base.OnStartup(e);

        // Setup system tray icon + menu
        SetupNotifyIcon();
        
        // Install to HKCU\...\Run for auto-start
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
        
        if (ServiceProvider is IDisposable disposable) disposable.Dispose(); 
        
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
        miRestart.Click += (s, a) => OnRestartService();

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
    
    private void OnOpenSettings()   { /* TODO */ }
    private void OnRestartService() { /* TODO */ }
    private void OnQuit()           { Shutdown(); }
}

