using System;
using System.IO;


namespace FoxyOverlay.Core;

/// <summary>
/// Every location the app writes to, in one place, so tests can redirect them and
/// the settings window can show the user where things live.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "FoxyOverlay";

    /// <summary>%AppData%\FoxyOverlay on Windows; ~/.config/FoxyOverlay elsewhere.</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        FolderName);

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");
    public static string StatsFile => Path.Combine(DataDirectory, "stats.json");
    public static string LogFile => Path.Combine(DataDirectory, "app.log");

    /// <summary>Where packs baked from user-supplied videos are cached.</summary>
    public static string PackCacheDirectory => Path.Combine(DataDirectory, "packs");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
