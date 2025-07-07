using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.UI
{
  public class SettingsViewModel : INotifyPropertyChanged
  {
    private readonly IConfigService    _configService;
    private readonly ILoggingService   _logger;

    public SettingsViewModel(IConfigService configService, ILoggingService logger)
    {
      _configService = configService;
      _logger        = logger;

      Logs = new ObservableCollection<string>();

      SaveCommand          = new RelayCommand(OnSave);
      CancelCommand        = new RelayCommand(OnCancel);
      ResetDefaultsCommand = new RelayCommand(OnResetDefaults);

      // TODO: load existing config asynchronously
      // _ = LoadAsync();
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion

    #region Config Properties
    private int _chanceX;
    public int ChanceX
    {
      get => _chanceX;
      set { if (value != _chanceX) { _chanceX = value; OnPropertyChanged(); } }
    }

    private int _cooldownSec;
    public int CooldownSec
    {
      get => _cooldownSec;
      set { if (value != _cooldownSec) { _cooldownSec = value; OnPropertyChanged(); } }
    }

    private string _videoPath = string.Empty;
    public string VideoPath
    {
      get => _videoPath;
      set { if (value != _videoPath) { _videoPath = value; OnPropertyChanged(); } }
    }

    private bool _isMuted;
    public bool IsMuted
    {
      get => _isMuted;
      set { if (value != _isMuted) { _isMuted = value; OnPropertyChanged(); } }
    }
    #endregion

    #region Commands
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    #endregion

    #region Logs
    public ObservableCollection<string> Logs { get; }
    #endregion

    #region Command Handlers (stubs)
    private void OnSave()
    {
      // TODO: gather properties into a Config, call _configService.SaveAsync(...)
    }

    private void OnCancel()
    {
      // TODO: close window or revert changes
    }

    private void OnResetDefaults()
    {
      ChanceX     = 10000;
      CooldownSec = 3;
      VideoPath   = string.Empty;
      IsMuted     = false;
    }
    #endregion

    // TODO: add async LoadAsync() to populate properties and Logs
  }
}