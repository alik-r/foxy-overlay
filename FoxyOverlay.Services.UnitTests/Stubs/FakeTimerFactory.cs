using System.Collections.Generic;
using System.Threading;

using FoxyOverlay.Services.Utils.Abstractions;
using ITimer = FoxyOverlay.Services.Utils.Abstractions.ITimer;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public sealed class FakeTimerFactory : ITimerFactory
{
    private readonly List<FakeTimer> _created = new List<FakeTimer>();

    /// <summary>The most recently created timer, which is the live one.</summary>
    public FakeTimer Current => _created[^1];

    public IReadOnlyList<FakeTimer> Created => _created;

    public ITimer Create(TimerCallback callback)
    {
        var timer = new FakeTimer { Callback = callback };
        _created.Add(timer);
        return timer;
    }
}
