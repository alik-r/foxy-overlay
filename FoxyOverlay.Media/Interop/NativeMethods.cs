using System;
using System.Runtime.InteropServices;


namespace FoxyOverlay.Media.Interop;

internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    /// <summary>Clicks pass straight through to whatever is underneath.</summary>
    public const int WS_EX_TRANSPARENT = 0x0000_0020;

    /// <summary>Keeps the overlay out of Alt-Tab.</summary>
    public const int WS_EX_TOOLWINDOW = 0x0000_0080;

    /// <summary>Never steal focus — the whole point is to interrupt without interrupting.</summary>
    public const int WS_EX_NOACTIVATE = 0x0800_0000;

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public override string ToString() => $"{Right - Left}x{Bottom - Top} at {Left},{Top}";
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // GetWindowLongPtr only exists as a real export on 64-bit; on 32-bit it is a macro
    // aliasing GetWindowLong, so pick at runtime rather than assuming a bitness.
    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    /// <summary>Adds extended window styles to an existing window.</summary>
    public static void AddExtendedStyle(IntPtr hWnd, int styles)
    {
        IntPtr current = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        // Cast through uint: the style bits are a bitmask, and sign-extending one
        // with the high bit set would flood the upper 32 bits with ones.
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(current.ToInt64() | (uint)styles));
    }
}
