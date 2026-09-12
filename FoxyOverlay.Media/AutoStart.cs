using System;
using System.Diagnostics;
using System.IO;

using Microsoft.Win32;


namespace FoxyOverlay.Media;

/// <summary>
/// "Start with Windows", via the per-user Run key. HKCU needs no elevation, which
/// matters on a machine where you may not be an administrator.
/// </summary>
public static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FoxyOverlay";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) != null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Returns true when the registry now matches <paramref name="enabled"/>.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string? executable = ExecutablePath();
            if (executable == null)
                return false;

            key.SetValue(ValueName, $"\"{executable}\"");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Path to the running .exe. Environment.ProcessPath is the apphost, which is what
    /// we want; MainModule can point at dotnet.exe for a framework-dependent launch.
    /// </summary>
    public static string? ExecutablePath()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return path;

        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
