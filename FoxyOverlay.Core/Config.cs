namespace FoxyOverlay.Core;

public class Config
{
    public int ChanceX { get; set; } = 10_000;
    public int CooldownSeconds { get; set; } = 3;
    public string VideoPath { get; set; } = string.Empty;
    public bool IsMuted { get; set; } = false;
}
