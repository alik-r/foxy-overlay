using System.Threading;
using System.Threading.Tasks;


namespace FoxyOverlay.Services.Abstractions;

/// <summary>
/// Whatever actually puts Foxy on the screen.
///
/// The service owns the full trigger -> play -> cooldown lifecycle and awaits this
/// call, rather than raising an event and hoping the handler reports back. The old
/// event-based version wedged permanently whenever the handler threw, which is why
/// the jumpscare only ever fired once.
/// </summary>
public interface IJumpscarePlayer
{
    /// <summary>
    /// Plays one jumpscare, returning when it has finished. Implementations should
    /// honour cancellation; the service cancels after <see cref="JumpscareService.PlaybackTimeout"/>.
    /// </summary>
    Task PlayAsync(CancellationToken cancellationToken);
}
