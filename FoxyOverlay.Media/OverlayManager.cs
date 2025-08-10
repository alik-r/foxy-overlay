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
                try
                {
                    me.Source      = new Uri(videoPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }
                
                me.IsMuted     = mute;
                me.Stretch     = Stretch.Uniform;
                // TODO: add chroma‐key shader
                me.MediaEnded += OnEnded;

                w.Show();
                me.Play();

                windows.Add(w);
            }
            
            Dispatcher.Run();
            return;

            void OnEnded(object sender, RoutedEventArgs e)
            {
                if (Interlocked.Decrement(ref remaining) != 0) return;
                foreach (var w in windows)
                {
                    var me = w.MediaElement;
                    me.MediaEnded -= OnEnded;
                    me.Source = null;
                    w.Close();
                }
                
                windows.Clear();

                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                tcs.TrySetResult(true);
            }
        });

        sta.SetApartmentState(ApartmentState.STA);
        sta.IsBackground = true;
        sta.Start();

        return tcs.Task;
    }
}