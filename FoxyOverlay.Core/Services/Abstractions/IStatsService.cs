using System.Threading.Tasks;


namespace FoxyOverlay.Core.Services.Abstractions;

public interface IStatsService
{
    /// <summary>A snapshot of the current counters; safe to read from any thread.</summary>
    Stats Current { get; }

    Task LoadAsync();

    void RecordRoll();
    void RecordJumpscare();
    void RecordError();

    /// <summary>Persists counters if they changed since the last flush.</summary>
    Task FlushAsync();
}
