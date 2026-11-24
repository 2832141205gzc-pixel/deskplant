using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

internal sealed class WateringEffect : IDisposable
{
    internal const int Duration = 1520;
    private const float CanWidth = 87;
    private const int FirstDropAt = 285, DropInterval = 65, DropFlight = 365, DropCount = 12;
    private readonly Bitmap can;
    private readonly float canHeight;
    private readonly PointF nozzle;

    internal WateringEffect(Decoration style)
    {
        nozzle = style.Id == "original-can" ? new PointF(-.43f, -.23f) : new PointF(-.44f, -.44f);
        using (var stream = typeof(WateringEffect).Assembly.GetManifestResourceStream(style.Resource))
        using (var image = new Bitmap(stream))
        using (var copy = image.Clone(new Rectangle(Point.Empty, image.Size), PixelFormat.Format32bppArgb))
        {
            var crop = PlantArtwork.CleanAndFindBounds(copy);
            canHeight = CanWidth * crop.Height / crop.Width;
            const int rasterWidth = 232;
            can = new Bitmap(rasterWidth, (int)Math.Round(rasterWidth * canHeight / CanWidth), PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(can))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(copy, new Rectangle(Point.Empty, can.Size), crop, GraphicsUnit.Pixel);
            }
        }
    }

    internal void Draw(Graphics graphics, PointF soil, long elapsed)
    {
        for (int i = 0; i < DropCount; i++)
        {
            int releaseAt = FirstDropAt + i * DropInterval;
            float flight = (elapsed - releaseAt) / (float)DropFlight;
            if (flight < 0 || flight >= 1) continue;
            var start = Nozzle(soil, releaseAt);
            var destination = soil.X + (i * 7 % 11 - 5) * 2.4f;
            float x = start.X + (destination - start.X) * flight;
            float y = start.Y + (soil.Y - start.Y) * flight * (.38f + .62f * flight);
            float size = 2 + i % 3 * .55f;
            int alpha = (int)(232 * Math.Min(1, (1 - flight) * 5));
            using (var border = new Pen(Color.FromArgb(alpha, 65, 139, 169), .65f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var fill = new SolidBrush(Color.FromArgb(alpha, 182, 231, 241)))
            {
                var drop = new RectangleF(x - size * .55f, y - size, size * 1.1f, size * 1.9f);
                graphics.FillEllipse(fill, drop);
                graphics.DrawEllipse(border, drop);
            }
        }

        float opacity, tilt;
        var center = Pose(soil, elapsed, out opacity, out tilt);
        if (opacity <= 0) return;
        var saved = graphics.Save();
        try
        {
            graphics.TranslateTransform(center.X, center.Y);
            graphics.RotateTransform(tilt);
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                var destination = new[] { new PointF(-CanWidth / 2, -canHeight / 2),
                                          new PointF(CanWidth / 2, -canHeight / 2),
                                          new PointF(-CanWidth / 2, canHeight / 2) };
                graphics.DrawImage(can, destination, new RectangleF(0, 0, can.Width, can.Height), GraphicsUnit.Pixel, attributes);
            }
        }
        finally { graphics.Restore(saved); }
    }

    private PointF Nozzle(PointF soil, long elapsed)
    {
        float opacity, tilt;
        var center = Pose(soil, elapsed, out opacity, out tilt);
        double angle = Math.PI * tilt / 180;
        float x = CanWidth * nozzle.X, y = canHeight * nozzle.Y;
        return new PointF(center.X + (float)(x * Math.Cos(angle) - y * Math.Sin(angle)),
                          center.Y + (float)(x * Math.Sin(angle) + y * Math.Cos(angle)));
    }

    private static PointF Pose(PointF soil, long elapsed, out float opacity, out float tilt)
    {
        var approach = Ease(elapsed / 260f);
        var depart = Ease((elapsed - 1100) / 420f);
        opacity = approach * (1 - depart);
        tilt = -36 * approach * (1 - depart);
        return new PointF(soil.X + 52 + 19 * (1 - approach + depart), soil.Y - 52 - 13 * (1 - approach + depart));
    }

    private static float Ease(float fraction)
    {
        var t = Math.Max(0, Math.Min(1, fraction));
        return t * t * (3 - 2 * t);
    }

    public void Dispose() { can.Dispose(); }
}
