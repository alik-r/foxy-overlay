using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Packs;
using FoxyOverlay.Core.Services.Abstractions;
using FoxyOverlay.Services;


namespace FoxyOverlay.Media;

public partial class SettingsWindow : Window
{
    private readonly IConfigService _configService;
    private readonly IStatsService _stats;
    private readonly ILoggingService _logger;
    private readonly JumpscareService _jumpscareService;
    private readonly PackBaker _packBaker;
    private readonly Func<Task> _onSaved;

    private Config _config = new Config();
    private bool _loaded;

    public SettingsWindow(
        IConfigService configService,
        IStatsService stats,
        ILoggingService logger,
        JumpscareService jumpscareService,
        PackBaker packBaker,
        Func<Task> onSaved)
    {
        InitializeComponent();

        _configService = configService;
        _stats = stats;
        _logger = logger;
        _jumpscareService = jumpscareService;
        _packBaker = packBaker;
        _onSaved = onSaved;

        Loaded += async (_, _) => await loadAsync();
    }

    private async Task loadAsync()
    {
        _config = await _configService.LoadAsync();

        ChanceBox.Text = _config.ChanceX.ToString(CultureInfo.InvariantCulture);
        CooldownBox.Text = _config.CooldownSeconds.ToString(CultureInfo.InvariantCulture);
        SizeSlider.Value = _config.HeightFraction;
        MuteCheck.IsChecked = _config.IsMuted;
        AllMonitorsCheck.IsChecked = _config.AllMonitors;
        EnabledCheck.IsChecked = _config.Enabled;
        PackBox.Text = _config.PackPath ?? string.Empty;
        AutoStartCheck.IsChecked = AutoStart.IsEnabled();

        _loaded = true;

        updateOddsHint();
        updateSizeLabel();
        updatePackHint();
        updateStats();

        PathsText.Text =
            $"Config:  {_configService.FilePath}{Environment.NewLine}" +
            $"Log:     {AppPaths.LogFile}{Environment.NewLine}" +
            $"Bundled: {PackLoader.BundledPackDirectory}";
    }

    private void updateStats()
    {
        Stats stats = _stats.Current;

        string last = stats.LastJumpscareUtc is { } when
            ? when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";

        string observed = double.IsNaN(stats.ObservedOddsDenominator)
            ? "not enough data"
            : $"1 in {stats.ObservedOddsDenominator:N0}";

        StatsText.Text =
            $"Scares:        {stats.JumpscaresTotal:N0}{Environment.NewLine}" +
            $"Rolls:         {stats.RollsTotal:N0}{Environment.NewLine}" +
            $"Observed rate: {observed}{Environment.NewLine}" +
            $"Last scare:    {last}{Environment.NewLine}" +
            $"Watching since:{stats.FirstRunUtc.ToLocalTime():yyyy-MM-dd}";
    }

    private void updateOddsHint()
    {
        if (!int.TryParse(ChanceBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int chance) ||
            chance < Config.MinChanceX)
        {
            OddsHint.Text = $"Enter a whole number of at least {Config.MinChanceX}.";
            return;
        }

        if (chance > Config.MaxChanceX)
        {
            OddsHint.Text = $"Maximum is {Config.MaxChanceX:N0}.";
            return;
        }

        var mean = TimeSpan.FromSeconds(Probability.MeanSecondsBetween(chance));
        var confident = TimeSpan.FromSeconds(Probability.SecondsUntilConfidence(chance));

        OddsHint.Text =
            $"About one scare every {Probability.Humanize(mean)} on average; " +
            $"99% likely within {Probability.Humanize(confident)}.";
    }

    private void updateSizeLabel() => SizeLabel.Text = $"{SizeSlider.Value:P0}";

    private void updatePackHint()
    {
        string path = PackBox.Text.Trim();

        if (string.IsNullOrEmpty(path))
        {
            PackHint.Text = "Using the bundled Foxy pack.";
            return;
        }

        if (PackLoader.IsPackDirectory(path))
        {
            PackHint.Text = "Baked pack folder.";
            return;
        }

        if (File.Exists(path))
        {
            PackHint.Text = _packBaker.IsAvailable
                ? "Green-screen video; it will be baked into frames on first use (needs a moment)."
                : "Green-screen video, but ffmpeg was not found on PATH. Install ffmpeg, or point at a baked pack folder.";
            return;
        }

        PackHint.Text = "Path not found; the bundled pack will be used instead.";
    }

    private void onChanceChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        updateOddsHint();
    }

    private void onSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        updateSizeLabel();
    }

    private void onBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pick a pack.json, or a green-screen video to bake",
            Filter = "Packs and videos|pack.json;*.mp4;*.mov;*.webm;*.avi;*.mkv|" +
                     "Baked pack (pack.json)|pack.json|" +
                     "Video files|*.mp4;*.mov;*.webm;*.avi;*.mkv|" +
                     "All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        // Picking the manifest means picking its folder; the config stores directories.
        PackBox.Text = Path.GetFileName(dialog.FileName).Equals(PackLoader.ManifestFileName, StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(dialog.FileName)!
            : dialog.FileName;

        updatePackHint();
    }

    private async void onSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(ChanceBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int chance))
            {
                StatusText.Text = "Chance must be a whole number.";
                return;
            }

            if (!int.TryParse(CooldownBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cooldown))
            {
                StatusText.Text = "Cooldown must be a whole number.";
                return;
            }

            _config.ChanceX = chance;
            _config.CooldownSeconds = cooldown;
            _config.HeightFraction = SizeSlider.Value;
            _config.IsMuted = MuteCheck.IsChecked ?? false;
            _config.AllMonitors = AllMonitorsCheck.IsChecked ?? true;
            _config.Enabled = EnabledCheck.IsChecked ?? true;
            _config.PackPath = string.IsNullOrWhiteSpace(PackBox.Text) ? null : PackBox.Text.Trim();

            bool wantAutoStart = AutoStartCheck.IsChecked ?? false;
            if (wantAutoStart != AutoStart.IsEnabled() && !AutoStart.SetEnabled(wantAutoStart))
            {
                StatusText.Text = "Saved, but 'start with Windows' could not be changed.";
                AutoStartCheck.IsChecked = AutoStart.IsEnabled();
            }

            _config.StartWithWindows = AutoStart.IsEnabled();

            await _configService.SaveAsync(_config);
            await _onSaved();

            // Validate() may have clamped values; show what was actually stored.
            ChanceBox.Text = _config.ChanceX.ToString(CultureInfo.InvariantCulture);
            CooldownBox.Text = _config.CooldownSeconds.ToString(CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(StatusText.Text) || StatusText.Text.StartsWith("Saved", StringComparison.Ordinal))
                StatusText.Text = $"Saved at {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed; see the log.";
            await _logger.LogErrorAsync($"settings save failed: {ex}");
        }
    }

    private async void onTest(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Playing...";
        try
        {
            await _jumpscareService.TriggerNowAsync();
            StatusText.Text = "Done.";
            updateStats();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Playback failed; see the log.";
            await _logger.LogErrorAsync($"test playback failed: {ex}");
        }
    }

    private void onOpenDataFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            Process.Start(new ProcessStartInfo(AppPaths.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception)
        {
            StatusText.Text = "Could not open the folder.";
        }
    }

    private void onClose(object sender, RoutedEventArgs e) => Close();
}
