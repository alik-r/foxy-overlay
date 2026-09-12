using System;
using System.Threading;

using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.UnitTests.Stubs;

/// <summary>A timer that never fires on its own; tests drive it explicitly.</summary>
public sealed class FakeTimer : ITimer
{
    public bool IsDisposed { get; private set; }
    public TimeSpan DueTime { get; private set; } = Timeout.InfiniteTimeSpan;
    public TimeSpan Period { get; private set; } = Timeout.InfiniteTimeSpan;
    public TimerCallback? Callback { get; set; }

    public void Change(TimeSpan dueTime, TimeSpan period)
    {
        DueTime = dueTime;
        Period = period;
    }

    /// <summary>Simulates the timer elapsing.</summary>
    public void Fire() => Callback?.Invoke(null);

    public void Dispose() => IsDisposed = true;
}
