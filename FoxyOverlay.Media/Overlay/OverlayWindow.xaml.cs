using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using FoxyOverlay.Media.Interop;


namespace FoxyOverlay.Media.Overlay;

/// <summary>
/// A borderless, click-through, always-on-top window that shows one frame at a time.
/// </summary>
public partial class OverlayWindow : Window
{
    private int _pendingX, _pendingY, _pendingWidth, _pendingHeight;
    private bool _hasPendingBounds;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += onSourceInitialized;
    }

    /// <summary>
    /// Positions the window in physical pixels. Screen bounds come from Win32 in
    /// physical pixels while WPF's Left/Top/Width/Height are DIPs, and converting
    /// between them across a mixed-DPI multi-monitor setup is a reliable source of
    /// half-off-screen overlays. SetWindowPos takes physical pixels directly.
    /// </summary>
    public void SetPhysicalBounds(int x, int y, int width, int height)
    {
        _pendingX = x;
        _pendingY = y;
        _pendingWidth = width;
        _pendingHeight = height;
        _hasPendingBounds = true;

        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            applyBounds(handle);
    }

    /// <summary>
    /// Points this window at the shared playback surface. Every overlay window
    /// draws the same WriteableBitmap, so a frame is written once no matter how
    /// many monitors are showing it.
    /// </summary>
    public void SetSurface(ImageSource surface) => FrameImage.Source = surface;

    /// <summary>
    /// Creates the underlying HWND without ever showing the window, so the expensive
    /// part of putting an overlay on screen happens long before a jumpscare fires.
    /// EnsureHandle rather than Show()/Hide(), which would flash on screen at startup.
    /// </summary>
    public void PrepareHidden() => new WindowInteropHelper(this).EnsureHandle();

    public void ShowOverlay()
    {
        Show();
        // Re-assert topmost: another window may have taken the top slot since the
        // last scare, and a stale Z-order would hide the overlay behind it.
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            applyBounds(handle);
    }

    public void HideOverlay() => Hide();

    /// <summary>Diagnostic description of where this window actually ended up.</summary>
    public string DescribeBounds()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        string actual = handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect)
            ? rect.ToString()
            : "<no handle>";

        return $"requested {_pendingWidth}x{_pendingHeight} at {_pendingX},{_pendingY}; " +
               $"actual {actual}; wpf {Width}x{Height} at {Left},{Top}; " +
               $"image {FrameImage.ActualWidth:0}x{FrameImage.ActualHeight:0}";
    }

    /// <summary>Really closes the window; <see cref="HideOverlay"/> only hides it.</summary>
    public void CloseOverlay() => Close();

    private void onSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExtendedStyle(
            handle,
            NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        if (_hasPendingBounds)
            applyBounds(handle);
    }

    /// <summary>
    /// Applies the requested bounds through both WPF and Win32.
    ///
    /// Win32 alone is not enough: WPF's own Left/Top/Width/Height stay NaN, so its
    /// layout pass re-sizes the window to the content's natural size and undoes the
    /// SetWindowPos. Setting both keeps the two in agreement, with SetWindowPos also
    /// re-asserting topmost.
    /// </summary>
    private void applyBounds(IntPtr handle)
    {
        double scaleX = 1.0, scaleY = 1.0;
        if (PresentationSource.FromVisual(this) is HwndSource { CompositionTarget: { } target })
        {
            Matrix fromDevice = target.TransformFromDevice;
            if (fromDevice.M11 > 0) scaleX = fromDevice.M11;
            if (fromDevice.M22 > 0) scaleY = fromDevice.M22;
        }

        Left = _pendingX * scaleX;
        Top = _pendingY * scaleY;
        Width = _pendingWidth * scaleX;
        Height = _pendingHeight * scaleY;

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            _pendingX, _pendingY, _pendingWidth, _pendingHeight,
            NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= onSourceInitialized;

        // Drop the reference to the shared surface. Note this clears the Image's
        // source, it does not null the field: nulling the XAML-generated field is
        // what made the old overlay throw on reuse.
        FrameImage.Source = null;

        base.OnClosed(e);
    }
}
