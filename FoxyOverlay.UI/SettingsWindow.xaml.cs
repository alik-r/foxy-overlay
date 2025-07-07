using System.Windows;

using Microsoft.Extensions.DependencyInjection;


namespace FoxyOverlay.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
    }
}