using System;
using System.Threading;

using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.Utils;

public class SystemTimer : ITimer
{
    private readonly Timer _timer;

    public SystemTimer(TimerCallback callback)
    {
        _timer = new Timer(callback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Change(TimeSpan dueTime, TimeSpan period)
    {
        _timer.Change(dueTime, period);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}