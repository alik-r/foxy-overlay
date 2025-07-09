using System;


namespace FoxyOverlay.Services.Utils.Abstractions;

public interface ITimer : IDisposable
{
    void Change(TimeSpan dueTime, TimeSpan period);
}