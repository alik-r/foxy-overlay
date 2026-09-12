using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Core;
using FoxyOverlay.Core.Services.Abstractions;


namespace FoxyOverlay.Services.UnitTests.Stubs;

public sealed class FakeStatsService : IStatsService
{
    private int _rolls;
    private int _jumpscares;
    private int _errors;

    public int Rolls => Volatile.Read(ref _rolls);
    public int Jumpscares => Volatile.Read(ref _jumpscares);
    public int Errors => Volatile.Read(ref _errors);

    public Stats Current => new Stats
    {
        RollsTotal = Rolls,
        JumpscaresTotal = Jumpscares,
        ErrorsTotal = Errors
    };

    public Task LoadAsync() => Task.CompletedTask;
    public void RecordRoll() => Interlocked.Increment(ref _rolls);
    public void RecordJumpscare() => Interlocked.Increment(ref _jumpscares);
    public void RecordError() => Interlocked.Increment(ref _errors);
    public Task FlushAsync() => Task.CompletedTask;
}
