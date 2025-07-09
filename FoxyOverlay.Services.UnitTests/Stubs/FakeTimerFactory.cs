using System.Threading;

using FoxyOverlay.Services.Utils.Abstractions;
using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public class FakeTimerFactory : ITimerFactory
{
    public FakeTimer TimerInstance { get; } = new FakeTimer();

    public ITimer Create(TimerCallback callback)
    {
        TimerInstance.OnChangeCallback = callback;
        return TimerInstance;
    }
}