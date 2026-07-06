using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace WgcSharp;

internal static class WgcBackend
{
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", PreserveSig = false)]
    private static extern void CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(IntPtr classId, [In] ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid, out IntPtr result);
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    public static Bitmap? Capture(IntPtr hwnd, int timeoutMs)
    {
        D3D11.D3D11CreateDevice(
            IntPtr.Zero, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            null!, out var d3dDevice, out var d3dContext).CheckError();

        using var _dev = d3dDevice;
        using var _ctx = d3dContext;

        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectable);
        var winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        Marshal.Release(inspectable);

        var item = CreateCaptureItemForWindow(hwnd);

        Bitmap? result = null;
        Exception? capturedException = null;
        using var frameReady = new ManualResetEventSlim(false);

        var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);

        var session = pool.CreateCaptureSession(item);
        try
        {
            var borderProp = session.GetType().GetProperty("IsBorderRequired");
            borderProp?.SetValue(session, false);
        }
        catch { }

        pool.FrameArrived += (sender, _) =>
        {
            if (frameReady.IsSet) return;
            var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            try
            {
                result = FrameToBitmap(frame, d3dDevice, d3dContext);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                SafeDispose(frame);
                frameReady.Set();
            }
        };

        session.StartCapture();
        frameReady.Wait(timeoutMs);

        SafeDispose(session);
        SafeDispose(pool);
        SafeDispose(winrtDevice);

        if (capturedException != null)
            throw new InvalidOperationException("WGC frame processing failed.", capturedException);

        return result;
    }

    private static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hwnd)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var hstring));
        try
        {
            var interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref interopGuid, out var factoryPtr));

            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            Marshal.Release(factoryPtr);

            var itemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            interop.CreateForWindow(hwnd, ref itemGuid, out var rawItem);

            var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(rawItem);
            Marshal.Release(rawItem);
            return item;
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    private static unsafe Bitmap FrameToBitmap(
        Direct3D11CaptureFrame frame, ID3D11Device device, ID3D11DeviceContext context)
    {
        var surfacePtr = MarshalInspectable<IDirect3DSurface>.FromManaged(frame.Surface);
        try
        {
            var accessGuid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
            Marshal.ThrowExceptionForHR(
                Marshal.QueryInterface(surfacePtr, in accessGuid, out var accessPtr));

            var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(accessPtr);
            Marshal.Release(accessPtr);

            var texGuid = typeof(ID3D11Texture2D).GUID;
            var texPtr = access.GetInterface(ref texGuid);

            using var frameTexture = new ID3D11Texture2D(texPtr);
            var desc = frameTexture.Description;

            var stagingDesc = new Texture2DDescription
            {
                Width = desc.Width, Height = desc.Height,
                MipLevels = 1, ArraySize = 1, Format = desc.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging, BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read, MiscFlags = ResourceOptionFlags.None
            };

            using var staging = device.CreateTexture2D(stagingDesc);
            context.CopyResource(staging, frameTexture);

            var mapped = context.Map(staging, 0, MapMode.Read);
            try
            {
                var bitmap = new Bitmap((int)desc.Width, (int)desc.Height, PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                for (int y = 0; y < bitmap.Height; y++)
                {
                    Buffer.MemoryCopy(
                        (void*)(mapped.DataPointer + y * mapped.RowPitch),
                        (void*)(bmpData.Scan0 + y * bmpData.Stride),
                        bmpData.Stride, bitmap.Width * 4);
                }

                bitmap.UnlockBits(bmpData);
                return bitmap;
            }
            finally
            {
                context.Unmap(staging, 0);
            }
        }
        finally
        {
            Marshal.Release(surfacePtr);
        }
    }

    private static void SafeDispose(object? obj)
    {
        if (obj == null) return;
        try
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }
        catch (InvalidCastException)
        {
            try { Marshal.FinalReleaseComObject(obj); } catch { }
        }
    }
}
