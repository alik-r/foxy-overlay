using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Extensions;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Services;
using FoxyOverlay.Services.Extensions;

using Microsoft.Win32;


namespace FoxyOverlay.Media
{
    public partial class MainWindow : Window
    {
        private readonly IConfigService _configService;
        private readonly JumpscareService _jumpscareService;
        private Config _config;

        public MainWindow()
        {
            InitializeComponent();

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddConfigService();
                    services.AddLoggingService();
                    services.AddSystemTimerFactory();
                    services.AddSingleton<JumpscareService>();
                }).Build();

            _configService = host.Services.GetRequiredService<IConfigService>();
            _jumpscareService = host.Services.GetRequiredService<JumpscareService>();

            Loaded += async (_, __) => await LoadConfig();
            
            // TODO: Inject overlayManager via DI
            OverlayManager overlayManager = new OverlayManager();
            _jumpscareService.JumpscareTriggered += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    await overlayManager.PlayAsync(_config.VideoPath, _config.IsMuted);
                });
            };
        }

        private async Task LoadConfig()
        {
            _config = await _configService.LoadAsync();
            ChanceXBox.Text = _config.ChanceX.ToString();
            CooldownBox.Text = _config.CooldownSeconds.ToString();
            VideoPathBox.Text = _config.VideoPath;
            MuteCheck.IsChecked = _config.IsMuted;

            this.updateGuaranteedTime();
        }

        private async void SaveRestart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ChanceXBox.Text, out var chance) || chance < 1) chance = 10000;
            if (!int.TryParse(CooldownBox.Text, out var cooldown) || cooldown < 0) cooldown = 3;

            _config.ChanceX = chance;
            _config.CooldownSeconds = cooldown;
            _config.VideoPath = VideoPathBox.Text;
            _config.IsMuted = MuteCheck.IsChecked ?? false;

            await _configService.SaveAsync(_config);
            await _jumpscareService.ResumeAsync();

            MessageBox.Show("Config saved and jumpscare service restarted.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mov;*.avi;*.wmv|All Files|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                VideoPathBox.Text = ofd.FileName;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void ChanceXBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            this.updateGuaranteedTime();
        }

        private void updateGuaranteedTime()
        {
            if (!int.TryParse(ChanceXBox.Text, out var chanceX) || chanceX <= 1)
            {
                GuaranteedTimeText.Text = "Enter a chance value greater than 1.";
                return;
            }

            double probNoTrigger = 1.0 - (1.0 / chanceX);
            double totalSeconds = Math.Ceiling(Math.Log(0.01) / Math.Log(probNoTrigger));
            if (double.IsInfinity(totalSeconds) || double.IsNaN(totalSeconds) || totalSeconds <= 0)
            {
                GuaranteedTimeText.Text = "Calculation error. Chance is too high or invalid.";
                return;
            }

            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
            var parts = new List<string>();
            if (time.Days > 0) parts.Add($"{time.Days} day{(time.Days > 1 ? "s" : "")}");
            if (time.Hours > 0) parts.Add($"{time.Hours} hour{(time.Hours > 1 ? "s" : "")}");
            if (time.Minutes > 0) parts.Add($"{time.Minutes} minute{(time.Minutes > 1 ? "s" : "")}");
            if (time.Seconds > 0) parts.Add($"{time.Seconds} second{(time.Seconds > 1 ? "s" : "")}");

            if (parts.Count == 0)
            {
                GuaranteedTimeText.Text = "A jumpscare is almost certain to happen instantly.";
            }
            else
            {
                GuaranteedTimeText.Text = $"A jumpscare is 99% likely to occur within {string.Join(", ", parts)}.";
            }
        }
    }
}