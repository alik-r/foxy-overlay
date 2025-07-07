using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using CommunityToolkit.Mvvm.Input;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.UI;

  public class SettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
  {
    private readonly IConfigService _configService;
    private readonly ILoggingService _logger;

    public SettingsViewModel(IConfigService configService, ILoggingService logger)
    {
      _configService = configService;
      _logger        = logger;

      Logs = new ObservableCollection<string>();

      BrowseCommand          = new AsyncRelayCommand(OnBrowseAsync);
      SaveCommand            = new AsyncRelayCommand(OnSaveAsync, CanSave);
      CancelCommand          = new RelayCommand(OnCancel);
      ResetDefaultsCommand   = new RelayCommand(OnResetDefaults);

      OnResetDefaults();
      _ = LoadAsync();
    }

    #region Properties

    private int _chanceX;
    public int ChanceX
    {
      get => _chanceX;
      set
      {
        if (value == _chanceX) return;
        _chanceX = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(TimeTo99999Percent));
        SaveCommand.NotifyCanExecuteChanged();
      }
    }

    private int _cooldownSeconds;
    public int CooldownSeconds
    {
      get => _cooldownSeconds;
      set
      {
        if (value == _cooldownSeconds) return;
        _cooldownSeconds = value;
        OnPropertyChanged();
        SaveCommand.NotifyCanExecuteChanged();
      }
    }

    private string _videoPath = string.Empty;
    public string VideoPath
    {
      get => _videoPath;
      set
      {
        if (value == _videoPath) return;
        _videoPath = value;
        OnPropertyChanged();
        SaveCommand.NotifyCanExecuteChanged();
      }
    }

    private bool _isMuted;
    public bool IsMuted
    {
      get => _isMuted;
      set
      {
        if (value == _isMuted) return;
        _isMuted = value;
        OnPropertyChanged();
      }
    }

    public ObservableCollection<string> Logs { get; }

    private string _videoPathError = string.Empty;
    public string VideoPathError
    {
      get => _videoPathError;
      private set
      {
        if (value == _videoPathError) return;
        _videoPathError = value;
        OnPropertyChanged();
        SaveCommand.NotifyCanExecuteChanged();
      }
    }

    public string TimeTo99999Percent
    {
      get
      {
        var secs = -Math.Log(1 - 0.99999) * ChanceX;
        return TimeSpan.FromSeconds(secs).ToString(@"mm\:ss");
      }
    }

    #endregion

    #region Commands

    public IAsyncRelayCommand BrowseCommand { get; }
    public IAsyncRelayCommand SaveCommand   { get; }
    public ICommand CancelCommand           { get; }
    public ICommand ResetDefaultsCommand    { get; }

    #endregion

    #region Loading

    private async Task LoadAsync()
    {
      var cfg = await _configService.LoadAsync();
      ChanceX     = cfg.ChanceX;
      CooldownSeconds = cfg.CooldownSeconds;
      VideoPath   = cfg.VideoPath;
      IsMuted     = cfg.IsMuted;
      VideoPathError = string.Empty;

      var lines = await _logger.ReadLogsAsync(500);
      Application.Current.Dispatcher.Invoke(() =>
      {
        Logs.Clear();
        foreach (var l in lines) Logs.Add(l);
      });
    }

    #endregion

    #region Browse

    private async Task OnBrowseAsync()
    {
      var dlg = new Microsoft.Win32.OpenFileDialog
      {
        Filter = "MP4 files (*.mp4)|*.mp4",
        Title  = "Select Video File"
      };
      if (dlg.ShowDialog() != true) return;

      var path = dlg.FileName;
      try
      {
        var player = new MediaPlayer();
        var tcs    = new TaskCompletionSource<bool>();
        player.MediaOpened += (_,__) => tcs.TrySetResult(true);
        player.MediaFailed += (_,e) => tcs.TrySetException(e.ErrorException);
        player.Open(new Uri(path));
        var first = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        if (first == tcs.Task && tcs.Task.IsCompletedSuccessfully)
        {
          VideoPath      = path;
          VideoPathError = string.Empty;
        }
        else
        {
          throw new InvalidOperationException("Media failed to open.");
        }
        player.Close();
      }
      catch (Exception ex)
      {
        VideoPathError = $"Cannot load video: {ex.Message}";
      }
    }

    #endregion

    #region Save / Reset / Cancel

    private bool CanSave()
    {
      if (ChanceX < 1 || ChanceX > 1_000_000) return false;
      if (CooldownSeconds < 0) return false;
      if (string.IsNullOrWhiteSpace(VideoPath)) return false;
      if (!File.Exists(VideoPath)) return false;
      if (!VideoPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        return false;
      if (!string.IsNullOrEmpty(VideoPathError)) return false;
      return true;
    }

    private async Task OnSaveAsync()
    {
      var cfg = new Config
      {
        ChanceX     = ChanceX,
        CooldownSeconds = CooldownSeconds,
        VideoPath   = VideoPath,
        IsMuted     = IsMuted
      };
      await _configService.SaveAsync(cfg);
      await _logger.LogInfoAsync("Settings saved");
      // TODO: propagate new config to running service
    }

    private void OnResetDefaults()
    {
      ChanceX        = 10000;
      CooldownSeconds    = 3;
      VideoPath      = string.Empty;
      IsMuted        = false;
      VideoPathError = string.Empty;
    }

    private void OnCancel()
    {
      var win = Application.Current.Windows
              .OfType<SettingsWindow>()
              .FirstOrDefault();
      win?.Close();
    }

    #endregion

    #region IDataErrorInfo

    public string Error => null!;

    public string this[string prop]
      => prop switch
      {
        nameof(ChanceX)     => (ChanceX < 1 || ChanceX > 1_000_000)
                               ? "Must be between 1 and 1,000,000"
                               : string.Empty,
        nameof(CooldownSeconds) => (CooldownSeconds < 0)
                               ? "Must be 0 or greater"
                               : string.Empty,
        _                   => string.Empty
      };

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
      => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    #endregion
  }