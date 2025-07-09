using System.Threading;

using FoxyOverlay.Services.Utils.Abstractions;
using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.Utils;

public class SystemTimerFactory : ITimerFactory
{
    public ITimer Create(TimerCallback callback)
    {
        return new SystemTimer(callback);
    }
}