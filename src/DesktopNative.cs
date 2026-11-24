using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

internal static class DesktopNative
{
    internal const int ToolWindow = 0x80;
    internal const int LayeredWindow = 0x80000;
    internal const int NoActivate = 0x08000000;
    internal const int MouseActivate = 0x21;
    internal const int DpiChanged = 0x02E0;
    internal static readonly int ShowPlantMessage = RegisterWindowMessage(AppEdition.StorageName + ".ShowWindow.1");

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string name);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr handle);

    internal static void ShowExistingPlant()
    {
        PostMessage(new IntPtr(0xFFFF), ShowPlantMessage, IntPtr.Zero, IntPtr.Zero);
    }
}

internal sealed class LayeredSurface : IDisposable
{
    private readonly IntPtr dc, dib, previous;
    private readonly Bitmap bitmap;

    internal Size Size { get { return bitmap.Size; } }

    internal LayeredSurface(int width, int height)
    {
        dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var header = new BitmapHeader {
                HeaderSize = (uint)Marshal.SizeOf(typeof(BitmapHeader)), Width = width,
                Height = -height, Planes = 1, Bits = 32
            };
            IntPtr pixels;
            dib = CreateDIBSection(dc, ref header, 0, out pixels, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(dc, dib);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, pixels);
        }
        catch
        {
            ReleaseHandles();
            throw;
        }
    }

    internal Graphics Draw() { return Graphics.FromImage(bitmap); }

    internal void Present(IntPtr window, Point position)
    {
        var source = Point.Empty;
        var size = Size;
        var blend = new Blend { Opacity = 255, AlphaFormat = 1 };
        UpdateLayeredWindow(window, IntPtr.Zero, ref position, ref size, dc, ref source, 0, ref blend, 2);
    }

    public void Dispose()
    {
        bitmap.Dispose();
        ReleaseHandles();
    }

    private void ReleaseHandles()
    {
        if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(dc, previous);
        if (dib != IntPtr.Zero) DeleteObject(dib);
        DeleteDC(dc);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        internal uint HeaderSize;
        internal int Width, Height;
        internal ushort Planes, Bits;
        internal uint Compression, ImageSize;
        internal int XResolution, YResolution;
        internal uint ColorsUsed, ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Blend
    {
        internal byte Operation, Flags, Opacity, AlphaFormat;
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapHeader header, uint usage,
        out IntPtr pixels, IntPtr section, uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr item);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destination, ref Point position,
        ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
}
