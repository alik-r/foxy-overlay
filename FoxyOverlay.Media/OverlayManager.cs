using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;


namespace FoxyOverlay.Media;

public class OverlayManager
{
    public Task PlayAsync(string videoPath, bool mute)
    {
        var tcs = new TaskCompletionSource<bool>();

        Thread sta = new Thread(() =>
        {
            var screens = Screen.AllScreens;
            int remaining = screens.Length;
            var windows = new List<OverlayWindow>();
            
            void OnEnded(object sender, RoutedEventArgs e)
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    foreach (var w in windows)
                        w.Close();
                    
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                    tcs.TrySetResult(true);
                }
            }
            
            foreach (var scr in screens)
            {
                var w = new OverlayWindow
                {
                    Left   = scr.Bounds.Left,
                    Top    = scr.Bounds.Top,
                    Width  = scr.Bounds.Width,
                    Height = scr.Bounds.Height
                };

                var me = w.MediaElement;
                me.Source      = new Uri(videoPath);
                me.IsMuted     = mute;
                me.Stretch     = Stretch.Uniform;
                // TODO: add chroma‐key shader
                me.MediaEnded += OnEnded;

                w.Show();
                me.Play();

                windows.Add(w);
            }
            
            Dispatcher.Run();
        });

        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();

        return tcs.Task;
    }
}