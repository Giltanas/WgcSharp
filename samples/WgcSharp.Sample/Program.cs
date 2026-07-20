using System.Diagnostics;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
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

// Optional: pass a logger to see detailed capture diagnostics
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger("WgcSharp");

var bitmap = WindowCapture.CaptureWindow(hwnd, CaptureStrategy.Auto, timeoutMs: 2000, logger: logger);
if (bitmap == null)
{
    Console.WriteLine("Capture failed — returned null.");
    return;
}

var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "capture.png");
var width = bitmap.Width;
var height = bitmap.Height;
bitmap.Save(outputPath, ImageFormat.Png);
bitmap.Dispose();

Console.WriteLine($"Saved: {outputPath} ({width}x{height})");
