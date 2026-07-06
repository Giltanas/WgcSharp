using System;
using System.Drawing;

namespace WgcSharp;

/// <summary>
/// Captures windows by HWND, including occluded, minimized, and DirectX/OpenGL game windows.
/// Uses Windows.Graphics.Capture (WGC) as primary method with DXGI Desktop Duplication fallback.
/// </summary>
public static class WindowCapture
{
    /// <summary>
    /// Captures a window as a <see cref="Bitmap"/>.
    /// Tries WGC first (works for occluded/minimized/DirectX windows),
    /// falls back to DXGI Desktop Duplication if WGC fails.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <param name="timeoutMs">Maximum time to wait for a frame, in milliseconds.</param>
    /// <returns>A <see cref="Bitmap"/> of the window content, or null if capture failed.</returns>
    public static Bitmap? CaptureWindow(IntPtr hwnd, int timeoutMs = 2000)
    {
        return CaptureWindow(hwnd, CaptureStrategy.Auto, timeoutMs);
    }

    /// <summary>
    /// Captures a window as a <see cref="Bitmap"/> using the specified strategy.
    /// </summary>
    /// <param name="hwnd">The window handle to capture.</param>
    /// <param name="strategy">Which capture backend to use.</param>
    /// <param name="timeoutMs">Maximum time to wait for a frame, in milliseconds.</param>
    /// <returns>A <see cref="Bitmap"/> of the window content, or null if capture failed.</returns>
    public static Bitmap? CaptureWindow(IntPtr hwnd, CaptureStrategy strategy, int timeoutMs = 2000)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));

        return strategy switch
        {
            CaptureStrategy.WgcOnly => WgcBackend.Capture(hwnd, timeoutMs),
            CaptureStrategy.DxgiOnly => DxgiBackend.Capture(hwnd, timeoutMs),
            _ => CaptureAuto(hwnd, timeoutMs)
        };
    }

    private static Bitmap? CaptureAuto(IntPtr hwnd, int timeoutMs)
    {
        try
        {
            var result = WgcBackend.Capture(hwnd, timeoutMs);
            if (result != null) return result;
        }
        catch
        {
            // Fall through to DXGI
        }

        return DxgiBackend.Capture(hwnd, timeoutMs);
    }
}
