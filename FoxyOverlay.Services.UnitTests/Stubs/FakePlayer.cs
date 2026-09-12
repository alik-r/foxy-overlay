using System;
using System.Threading;
using System.Threading.Tasks;

using FoxyOverlay.Services.Abstractions;


namespace FoxyOverlay.Services.UnitTests.Stubs;

/// <summary>A player whose behaviour each test dictates.</summary>
public sealed class FakePlayer : IJumpscarePlayer
{
    private int _playCount;

    public int PlayCount => Volatile.Read(ref _playCount);

    /// <summary>Thrown from PlayAsync when set, to exercise the failure paths.</summary>
    public Exception? ThrowOnPlay { get; set; }

    /// <summary>When true, PlayAsync blocks until cancelled — a wedged player.</summary>
    public bool HangUntilCancelled { get; set; }

    /// <summary>Observed by tests that assert the service concurrency guard holds.</summary>
    public int MaxConcurrentPlays { get; private set; }

    private int _concurrent;

    public async Task PlayAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _playCount);

        int now = Interlocked.Increment(ref _concurrent);
        MaxConcurrentPlays = Math.Max(MaxConcurrentPlays, now);

        try
        {
            if (ThrowOnPlay != null)
                throw ThrowOnPlay;

            if (HangUntilCancelled)
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);

            await Task.Yield();
        }
        finally
        {
            Interlocked.Decrement(ref _concurrent);
        }
    }
}
