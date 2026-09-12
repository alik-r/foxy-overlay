namespace FoxyOverlay.Services.Enums;

public enum JumpscareState
{
    /// <summary>Rolling once a second.</summary>
    Idle,

    /// <summary>An overlay is on screen.</summary>
    Playing,

    /// <summary>Playback finished; waiting out <see cref="Core.Config.CooldownSeconds"/>.</summary>
    Cooldown,

    /// <summary>Rolling suspended by the user.</summary>
    Paused,

    /// <summary>Service stopped; the timer is gone.</summary>
    Stopped
}
