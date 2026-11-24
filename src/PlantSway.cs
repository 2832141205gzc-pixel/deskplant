using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

internal sealed class PlantSway : IDisposable
{
    internal const int FrameInterval = 42;
    private const int Padding = 18;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly Bitmap bitmap;
    private readonly int[] sourcePixels, framePixels;
    private readonly int innerWidth, firstLeaf, soilLine;
    private long lastFrame = -1;

    internal PlantSway(Bitmap source, Rectangle bounds, Rectangle stageBounds, double foliageBottom)
    {
        var reduction = Math.Min(360.0 / bounds.Width, 434.0 / bounds.Height);
        innerWidth = (int)Math.Round(bounds.Width * reduction);
        var height = (int)Math.Round(bounds.Height * reduction);
        bitmap = new Bitmap(innerWidth + Padding * 2, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Rectangle(Padding, 0, innerWidth, height), bounds, GraphicsUnit.Pixel);
        }

        var surface = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
        try
        {
            sourcePixels = new int[bitmap.Width * height];
            for (int y = 0; y < height; y++)
                Marshal.Copy(IntPtr.Add(surface.Scan0, y * surface.Stride), sourcePixels, y * bitmap.Width, bitmap.Width);
        }
        finally { bitmap.UnlockBits(surface); }

        framePixels = (int[])sourcePixels.Clone();
        firstLeaf = Math.Max(0, (int)Math.Round((stageBounds.Top - bounds.Top) * reduction) - 2);
        soilLine = (int)Math.Round(height * foliageBottom);
    }

    internal void Draw(Graphics graphics, RectangleF destination)
    {
        var frame = Clock.ElapsedMilliseconds / FrameInterval;
        if (frame != lastFrame)
        {
            UpdateFrame(frame * FrameInterval / 1000.0);
            lastFrame = frame;
        }

        var gutter = destination.Width * Padding / innerWidth;
        destination.Inflate(gutter, 0);
        graphics.DrawImage(bitmap, destination, new Rectangle(Point.Empty, bitmap.Size), GraphicsUnit.Pixel);
    }

    private void UpdateFrame(double t)
    {
        // Ease the shift down to zero at the rim, keeping the flowerpot and soil anchored.
        var stem = soilLine - firstLeaf;
        if (stem <= 0) return;
        var wind = t * (2 * Math.PI / 4.6);
        var amplitude = Math.Min(13, stem * .09);
        var width = bitmap.Width;
        for (int y = firstLeaf; y < soilLine; y++)
        {
            var strength = (soilLine - y) / (double)stem;
            var shift = -amplitude * strength * strength * (.84 * Math.Sin(wind) + .16 * Math.Sin(wind * 2 - strength));
            var offset = (int)Math.Floor(shift);
            var fraction = (uint)Math.Round((shift - offset) * 256);
            var row = y * width;
            Array.Clear(framePixels, row, width);
            for (int x = Math.Max(0, -offset); x < Math.Min(width, width - 1 - offset); x++)
            {
                var sourceIndex = row + x + offset;
                framePixels[row + x] = Mix(sourcePixels[sourceIndex], sourcePixels[sourceIndex + 1], fraction);
            }
        }

        var surface = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
        try
        {
            for (int y = firstLeaf; y < soilLine; y++)
                Marshal.Copy(framePixels, y * width, IntPtr.Add(surface.Scan0, y * surface.Stride), width);
        }
        finally { bitmap.UnlockBits(surface); }
    }

    private static int Mix(int left, int right, uint rightWeight)
    {
        // Interpolate premultiplied color and alpha together to preserve translucent leaf edges.
        const uint mask = 0x00ff00ff;
        var a = (uint)left;
        var b = (uint)right;
        var leftWeight = 256 - rightWeight;
        var redBlue = (((a & mask) * leftWeight + (b & mask) * rightWeight) >> 8) & mask;
        var alphaGreen = ((((a >> 8) & mask) * leftWeight + ((b >> 8) & mask) * rightWeight) >> 8) & mask;
        return (int)(redBlue | (alphaGreen << 8));
    }

    public void Dispose() { bitmap.Dispose(); }
}
