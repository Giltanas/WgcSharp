# WgcSharp

Capture any window by HWND — including occluded, minimized, and DirectX/OpenGL game windows.

Uses **Windows.Graphics.Capture** (WGC) as primary capture method with **DXGI Desktop Duplication** fallback. Handles all the COM/WinRT interop complexity so you don't have to.

## Why?

- `PrintWindow` returns black for DirectX/OpenGL game windows
- `BitBlt`/`CopyFromScreen` can't capture hardware-accelerated content
- DXGI Desktop Duplication only captures visible (non-occluded) windows
- Using `Windows.Graphics.Capture` directly from C# requires painful COM interop with CsWinRT, HSTRING marshaling, and ComWrappers registration

**WgcSharp** wraps all of this into a single method call.

## Install

```
dotnet add package WgcSharp
```

## Usage

```csharp
using WgcSharp;

// Capture any window by its handle
var bitmap = WindowCapture.CaptureWindow(hwnd);
bitmap.Save("screenshot.png");
```

### Choose a strategy

```csharp
// WGC only — works for occluded/minimized/DirectX windows (Win10 1803+)
var bitmap = WindowCapture.CaptureWindow(hwnd, CaptureStrategy.WgcOnly);

// DXGI only — captures visible windows via Desktop Duplication
var bitmap = WindowCapture.CaptureWindow(hwnd, CaptureStrategy.DxgiOnly);

// Auto (default) — tries WGC first, falls back to DXGI
var bitmap = WindowCapture.CaptureWindow(hwnd, CaptureStrategy.Auto);
```

### Full example

```csharp
using System.Diagnostics;
using System.Drawing.Imaging;
using WgcSharp;

var process = Process.GetProcessesByName("notepad").First();
var bitmap = WindowCapture.CaptureWindow(process.MainWindowHandle);
bitmap?.Save("notepad.png", ImageFormat.Png);
```

## How it works

### WGC Backend (primary)
1. Creates a D3D11 device and wraps it as a WinRT `IDirect3DDevice`
2. Creates a `GraphicsCaptureItem` for the target HWND via COM interop (`IGraphicsCaptureItemInterop`)
3. Sets up a `Direct3D11CaptureFramePool` and capture session
4. Captures a single frame, extracts the D3D11 texture via `IDirect3DDxgiInterfaceAccess`
5. Copies to a staging texture and reads pixels into a `System.Drawing.Bitmap`

All WinRT/COM marshaling is handled internally — no CsWinRT `ComWrappers` conflicts, no HSTRING issues.

### DXGI Backend (fallback)
1. Creates a D3D11 device and finds the DXGI output for the target window's monitor
2. Uses `IDXGIOutput1.DuplicateOutput` to capture the full desktop frame
3. Crops to the target window's screen coordinates (via `DwmGetWindowAttribute`)

## Requirements

- Windows 10 version 1803+ (build 17134) for WGC
- .NET 8.0+, .NET 9.0+, or .NET 10.0+ (Windows TFM)
- Hardware GPU (D3D11)

## License

MIT
