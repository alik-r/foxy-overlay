using System;
using System.Threading;

using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public class FakeTimer : ITimer
{
    public bool IsDisposed { get; private set; }
    public TimerCallback? OnChangeCallback { get; set; }

    public void Change(TimeSpan dueTime, TimeSpan period)
    {
    }

    public void Dispose() => IsDisposed = true;
}