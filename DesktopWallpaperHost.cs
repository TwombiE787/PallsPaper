using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PallsPaper;

internal static class DesktopWallpaperHost
{
    private const uint ProgmanWorkerMessage = 0x052C;
    private const uint SmtoNormal = 0x0000;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public static void Attach(Form form, Rectangle bounds)
    {
        form.Bounds = bounds;
        var desktop = FindWallpaperHost();
        if (desktop == IntPtr.Zero)
        {
            throw new InvalidOperationException("Das Windows-Desktopfenster für das Wallpaper wurde nicht gefunden.");
        }

        var handle = form.Handle;
        SetWindowLongPtr(handle, GwlStyle, (GetWindowLongPtr(handle, GwlStyle).ToInt64() & ~WsPopup) | WsChild);
        Marshal.SetLastPInvokeError(0);
        if (SetParent(handle, desktop) == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Das Wallpaper konnte nicht an den Desktop angehängt werden.");
        }

        form.Show();
        var topLeft = bounds.Location;
        MapWindowPoints(IntPtr.Zero, desktop, ref topLeft, 1);
        if (!SetWindowPos(handle, IntPtr.Zero, topLeft.X, topLeft.Y, bounds.Width, bounds.Height,
                SwpFrameChanged | SwpNoActivate | SwpShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Das Wallpaperfenster konnte nicht positioniert werden.");
        }
    }

    private static IntPtr FindWallpaperHost()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            SendMessageTimeout(progman, ProgmanWorkerMessage, IntPtr.Zero, IntPtr.Zero, SmtoNormal, 1000, out _);
        }

        var wallpaperHost = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                wallpaperHost = FindWindowEx(IntPtr.Zero, window, "WorkerW", null);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return wallpaperHost != IntPtr.Zero ? wallpaperHost : progman;
    }

    private static void EnumWindows(EnumWindowsCallback callback, IntPtr lParam)
    {
        _ = EnumWindowsNative(callback, lParam);
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "EnumWindows", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindowsNative(EnumWindowsCallback callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, long value);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : new IntPtr(GetWindowLong32(window, index));

    private static void SetWindowLongPtr(IntPtr window, int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previousValue = IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : new IntPtr(SetWindowLong32(window, index, unchecked((int)value)));

        if (previousValue == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Der Fensterstil des Wallpapers konnte nicht angepasst werden.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref Point points, uint pointCount);
}
