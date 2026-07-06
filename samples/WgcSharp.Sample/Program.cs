using System.Diagnostics;
using System.Drawing.Imaging;
using WgcSharp;

Console.WriteLine("WgcSharp — Window Capture Demo");
Console.WriteLine("==============================");
Console.WriteLine();

// Find a window to capture
Console.Write("Enter process name (e.g. notepad, chrome): ");
var processName = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(processName))
{
    Console.WriteLine("No process name entered.");
    return;
}

var process = Process.GetProcessesByName(processName).FirstOrDefault();
if (process == null)
{
    Console.WriteLine($"Process '{processName}' not found.");
    return;
}

var hwnd = process.MainWindowHandle;
if (hwnd == IntPtr.Zero)
{
    Console.WriteLine($"Process '{processName}' has no visible window.");
    return;
}

Console.WriteLine($"Found: {process.MainWindowTitle} (HWND: 0x{hwnd:X})");
Console.WriteLine("Capturing...");

var bitmap = WindowCapture.CaptureWindow(hwnd);
if (bitmap == null)
{
    Console.WriteLine("Capture failed — returned null.");
    return;
}

var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "capture.png");
bitmap.Save(outputPath, ImageFormat.Png);
bitmap.Dispose();

Console.WriteLine($"Saved: {outputPath} ({bitmap.Width}x{bitmap.Height})");
