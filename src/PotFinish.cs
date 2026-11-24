using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

internal static class PotFinish
{
    internal static void Apply(Bitmap plant, Rectangle frame, Rectangle shared, double soil, Bitmap texture, bool leavesDrapeOverPot)
    {
        // Use the existing silhouette to keep rims, soil and roots aligned across every stage.
        int top = shared.Top + (int)(shared.Height * (soil + .075));
        int bottom = frame.Bottom;
        if (bottom <= top + 8) return;
        var paint = new int[texture.Width * texture.Height];
        var raster = texture.LockBits(new Rectangle(Point.Empty, texture.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < texture.Height; y++)
                Marshal.Copy(IntPtr.Add(raster.Scan0, y * raster.Stride), paint, y * texture.Width, texture.Width);
        }
        finally { texture.UnlockBits(raster); }

        var row = new int[plant.Width];
        var plantData = plant.LockBits(new Rectangle(Point.Empty, plant.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = top; y < bottom; y++)
            {
                var address = IntPtr.Add(plantData.Scan0, y * plantData.Stride);
                Marshal.Copy(address, row, 0, row.Length);
                float height = (y - top) / (float)(bottom - top);
                int paintRow = (int)(texture.Height * (.28f + .67f * height)) * texture.Width;
                int min, max, paintMin, paintMax;
                if (!Span(row, 0, frame.Left, frame.Right, out min, out max) ||
                    !Span(paint, paintRow, 0, texture.Width, out paintMin, out paintMax)) continue;
                float opacity = Math.Min(1, height * 11) * .95f;
                for (int x = min; x <= max; x++)
                {
                    int original = row[x];
                    if ((uint)original >> 24 < 16) continue;
                    int r = original >> 16 & 255, g = original >> 8 & 255, b = original & 255;
                    if (leavesDrapeOverPot && height < .36f && g > b + 14 && g > r + 4) continue;
                    int patternX = paintMin + (x - min) * (paintMax - paintMin) / Math.Max(1, max - min);
                    int pigment = paint[paintRow + patternX];
                    int light = (r * 3 + g * 6 + b) / 10;
                    row[x] = original & unchecked((int)0xFF000000) |
                             Blend(r, pigment >> 16 & 255, light, opacity) << 16 |
                             Blend(g, pigment >> 8 & 255, light, opacity) << 8 |
                             Blend(b, pigment & 255, light, opacity);
                }
                Marshal.Copy(row, 0, address, row.Length);
            }
        }
        finally { plant.UnlockBits(plantData); }
    }

    private static bool Span(int[] row, int offset, int left, int right, out int start, out int end)
    {
        start = left;
        end = right - 1;
        while (start < end && (uint)row[offset + start] >> 24 < 120) start++;
        while (end > start && (uint)row[offset + end] >> 24 < 120) end--;
        return start < end;
    }

    private static int Blend(int original, int pigment, int light, float opacity)
    {
        var target = Math.Max(0, Math.Min(255, pigment + (light - 122) * .12f));
        return (int)(original + (target - original) * opacity);
    }
}
