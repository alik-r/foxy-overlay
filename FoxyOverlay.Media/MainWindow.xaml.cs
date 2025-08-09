using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Threading.Tasks;
using System.Windows;

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
    }
}
