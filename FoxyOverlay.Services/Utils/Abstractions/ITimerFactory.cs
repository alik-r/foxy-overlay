using System.Threading;


namespace FoxyOverlay.Services.Utils.Abstractions;

public interface ITimerFactory
{
    ITimer Create(TimerCallback callback);
}