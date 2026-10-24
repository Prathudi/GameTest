using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GameTest.Runner;

/// <summary>
/// Saves a picture of a window's drawing area. It copies the pixels from the screen,
/// so it shows what a person would see: the window must be visible and not covered.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowCapture
{
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    /// <summary>
    /// Makes this program report real screen pixels on scaled displays (125%, 150%...).
    /// Without it the captured area could be offset or cropped. Call it once, early.
    /// </summary>
    public static void MakeProcessDpiAware() => SetProcessDPIAware();

    /// <summary>Waits until the game has a window, and returns its handle.</summary>
    public static async Task<IntPtr> WaitForWindowAsync(GameProcess game, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            IntPtr handle = game.MainWindowHandle;
            if (handle != IntPtr.Zero) return handle;
            if (game.HasExited) throw new ScreenshotException("the game exited before it showed a window");
            if (DateTime.UtcNow >= deadline)
            {
                throw new ScreenshotException($"the game showed no window within {timeout.TotalSeconds:0.#} s");
            }
            await Task.Delay(50);
        }
    }

    /// <summary>Saves the window's drawing area (without title bar and borders) as a PNG.</summary>
    public static async Task CaptureClientAreaAsync(IntPtr window, string pngPath)
    {
        if (window == IntPtr.Zero || !IsWindow(window))
        {
            throw new ScreenshotException("the game window does not exist (the game may have closed or crashed)");
        }

        // A minimized window has no pixels on screen. Restore it and bring it to the front.
        if (IsIconic(window)) ShowWindow(window, SW_RESTORE);
        SetForegroundWindow(window);  // Windows may refuse this; the capture is still tried
        await Task.Delay(150);        // give the window time to be drawn on screen

        if (IsIconic(window)) throw new ScreenshotException("the game window is minimized and could not be restored");
        if (!GetClientRect(window, out NativeRect rect)) throw new ScreenshotException("could not read the window size");

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) throw new ScreenshotException($"the window has no visible area ({width}x{height})");

        var origin = new NativePoint { X = 0, Y = 0 };
        if (!ClientToScreen(window, ref origin)) throw new ScreenshotException("could not find the window's position on screen");

        try
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(pngPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            bitmap.Save(pngPath, ImageFormat.Png);
        }
        catch (ExternalException ex)  // includes Win32Exception: typically a locked screen or no desktop
        {
            throw new ScreenshotException(
                $"the screen could not be copied (is the screen locked, or is there no desktop session?): {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ScreenshotException($"the picture could not be saved to {pngPath}: {ex.Message}");
        }
    }
}
