using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

internal sealed class FruitPileArtwork : IDisposable
{
    private static readonly string[] AtlasOrder = { "passion-fruit", "pineapple", "blueberry", "strawberry", "kumquat", "cherry-tomato" };

    // X is sideways, depth is towards the viewer, and lift is above the shelf.
    private static readonly Seat[] Heap = {
        new Seat(-.12f, -.54f, 0), new Seat(-.53f, .25f, 0), new Seat(.51f, .22f, 0), new Seat(.11f, -.1f, .63f),
        new Seat(.91f, -.37f, 0), new Seat(-.97f, -.30f, 0), new Seat(-.08f, .87f, 0), new Seat(-.57f, -.46f, .57f),
        new Seat(.60f, -.51f, .58f), new Seat(-.10f, -.34f, 1.17f), new Seat(-1.34f, .26f, 0), new Seat(1.42f, .31f, 0),
        new Seat(-.74f, .74f, 0), new Seat(.67f, .72f, 0), new Seat(.82f, .11f, .64f), new Seat(-.83f, .17f, .58f),
        new Seat(.34f, .38f, .60f), new Seat(-.31f, .41f, .62f), new Seat(-1.62f, -.28f, 0), new Seat(1.60f, -.21f, 0),
        new Seat(.53f, -.85f, 1.06f), new Seat(-.60f, -.86f, 1.02f), new Seat(.39f, .03f, 1.21f), new Seat(-.33f, .07f, 1.27f)
    };

    private Bitmap[] fruit;
    private PointF[][] edges;

    internal void DrawSingle(Graphics graphics, PlantSpecies crop, RectangleF area, bool green)
    {
        var index = Array.IndexOf(AtlasOrder, crop.Id);
        if (index < 0) return;
        EnsureLoaded();
        var sprite = fruit[index];
        var scale = Math.Min(area.Width / sprite.Width, area.Height / sprite.Height);
        var width = sprite.Width * scale;
        var height = sprite.Height * scale;
        var target = new RectangleF(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        if (!green)
        {
            graphics.DrawImage(sprite, target);
            return;
        }

        using (var tint = new ImageAttributes())
        {
            tint.SetColorMatrix(new ColorMatrix(new[] {
                new[] { .13f, .20f, .07f, 0, 0 }, new[] { .30f, .46f, .15f, 0, 0 },
                new[] { .05f, .08f, .03f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { .30f, .35f, .11f, 0, 1 }
            }));
            graphics.DrawImage(sprite, new[] { new PointF(target.Left, target.Top), new PointF(target.Right, target.Top),
                new PointF(target.Left, target.Bottom) }, new RectangleF(0, 0, sprite.Width, sprite.Height), GraphicsUnit.Pixel, tint);
        }
    }

    internal void Draw(Graphics graphics, PlantSpecies crop, long quantity, RectangleF area)
    {
        var index = Array.IndexOf(AtlasOrder, crop.Id);
        if (index < 0 || quantity <= 0) return;
        EnsureLoaded();
        var sprite = fruit[index];
        var count = (int)Math.Min(Heap.Length, quantity);
        var random = new Random(4127 + index * 593);
        var pieces = new Piece[count];
        float widthRatio = sprite.Width / (float)sprite.Height;
        float spacing = Math.Max(.60f, Math.Min(1, widthRatio));
        for (int i = 0; i < count; i++)
        {
            var seat = count == 1 ? new Seat(0, 0, 0) : count == 2
                ? new Seat(i == 0 ? -.42f : .43f, i == 0 ? -.22f : .24f, 0) : Heap[i];
            var size = .87f + (float)random.NextDouble() * .16f;
            var width = size * Math.Min(1, widthRatio);
            var height = size * Math.Min(1, 1 / widthRatio);
            var angle = (float)(random.NextDouble() - .5) * (crop.Id == "pineapple" ? 54 : 76);
            pieces[i] = new Piece(seat.X * spacing + (float)(random.NextDouble() - .5) * .10f,
                seat.Depth * .34f - seat.Lift * .62f - height * .35f,
                seat.Depth + .30f * seat.Lift, width, height, angle, seat.Lift == 0, edges[index]);
        }

        var footprint = pieces.Select(piece => piece.Bounds).Aggregate(RectangleF.Union);
        float scale = Math.Min(count == 1 ? 61 : 56,
            Math.Min((area.Width - 10) / footprint.Width, (area.Height - 11) / footprint.Height));
        float originX = area.X + area.Width / 2 - (footprint.Left + footprint.Width / 2) * scale;
        float originY = area.Bottom - 6 - footprint.Bottom * scale;

        if (count > 3) DrawShadow(graphics, new RectangleF(area.X + (area.Width - footprint.Width * scale * .85f) / 2,
            area.Bottom - 14, footprint.Width * scale * .85f, 13), 48);
        foreach (var piece in pieces)
        {
            if (!piece.OnGround) continue;
            float width = piece.Width * scale * .76f;
            float bottom = originY + piece.Bounds.Bottom * scale;
            DrawShadow(graphics, new RectangleF(originX + (piece.Bounds.Left + piece.Bounds.Width / 2) * scale - width / 2,
                bottom - 4, width, Math.Max(4, width * .18f)), 89);
        }

        using (var tone = new ImageAttributes())
        {
            foreach (var piece in pieces.OrderBy(piece => piece.Depth))
            {
                var transform = graphics.Save();
                graphics.TranslateTransform(originX + piece.X * scale, originY + piece.Y * scale);
                graphics.RotateTransform(piece.Angle);
                float halfWidth = piece.Width * scale / 2, halfHeight = piece.Height * scale / 2;
                float light = Math.Max(.82f, Math.Min(1, .94f + piece.Depth * .09f));
                tone.SetColorMatrix(new ColorMatrix { Matrix00 = light, Matrix11 = light, Matrix22 = light });
                graphics.DrawImage(sprite, new[] { new PointF(-halfWidth, -halfHeight), new PointF(halfWidth, -halfHeight),
                    new PointF(-halfWidth, halfHeight) }, new RectangleF(0, 0, sprite.Width, sprite.Height), GraphicsUnit.Pixel, tone);
                graphics.Restore(transform);
            }
        }
    }

    private static void DrawShadow(Graphics graphics, RectangleF shadow, int opacity)
    {
        using (var outline = new GraphicsPath())
        {
            outline.AddEllipse(shadow);
            using (var brush = new PathGradientBrush(outline))
            {
                brush.CenterColor = Color.FromArgb(opacity, 59, 45, 29);
                brush.SurroundColors = new[] { Color.FromArgb(0, 59, 45, 29) };
                graphics.FillEllipse(brush, shadow);
            }
        }
    }

    private void EnsureLoaded()
    {
        if (fruit != null) return;
        fruit = new Bitmap[AtlasOrder.Length];
        edges = new PointF[AtlasOrder.Length][];
        using (var stream = typeof(FruitPileArtwork).Assembly.GetManifestResourceStream("DeskPlant.WarehouseFruit"))
        using (var atlas = new Bitmap(stream))
        {
            int width = atlas.Width / 3, height = atlas.Height / 2;
            for (int i = 0; i < fruit.Length; i++)
            {
                using (var cell = atlas.Clone(new Rectangle(i % 3 * width, i / 3 * height, width, height), PixelFormat.Format32bppArgb))
                {
                    var bounds = PlantArtwork.CleanAndFindBounds(cell, 24);
                    fruit[i] = cell.Clone(bounds, PixelFormat.Format32bppArgb);
                    edges[i] = ReadEdges(fruit[i]);
                }
            }
        }
    }

    private static PointF[] ReadEdges(Bitmap image)
    {
        // Each row's silhouette endpoints suffice for the extrema of any rotated frame.
        var points = new List<PointF>();
        var pixels = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[image.Width * 4];
            for (int y = 0; y < image.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(pixels.Scan0, y * pixels.Stride), row, 0, row.Length);
                int left = 0, right = image.Width - 1;
                while (left <= right && row[left * 4 + 3] < 64) left++;
                if (left > right) continue;
                while (row[right * 4 + 3] < 64) right--;
                points.Add(new PointF(left / (float)image.Width - .5f, y / (float)image.Height - .5f));
                points.Add(new PointF(right / (float)image.Width - .5f, y / (float)image.Height - .5f));
            }
        }
        finally { image.UnlockBits(pixels); }
        return points.ToArray();
    }

    public void Dispose()
    {
        if (fruit == null) return;
        foreach (var sprite in fruit) if (sprite != null) sprite.Dispose();
        fruit = null;
    }

    private struct Seat
    {
        internal readonly float X, Depth, Lift;
        internal Seat(float x, float depth, float lift) { X = x; Depth = depth; Lift = lift; }
    }

    private struct Piece
    {
        internal readonly float X, Y, Depth, Width, Height, Angle;
        internal readonly bool OnGround;
        internal readonly RectangleF Bounds;
        internal Piece(float x, float y, float depth, float width, float height, float angle, bool onGround, PointF[] outline)
        {
            X = x; Y = y; Depth = depth; Width = width; Height = height; Angle = angle; OnGround = onGround;
            var radians = Math.PI * Angle / 180;
            var cosine = (float)Math.Cos(radians);
            var sine = (float)Math.Sin(radians);
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            foreach (var point in outline)
            {
                var rotatedX = X + point.X * Width * cosine - point.Y * Height * sine;
                var rotatedY = Y + point.X * Width * sine + point.Y * Height * cosine;
                left = Math.Min(left, rotatedX);
                right = Math.Max(right, rotatedX);
                top = Math.Min(top, rotatedY);
                bottom = Math.Max(bottom, rotatedY);
            }
            Bounds = RectangleF.FromLTRB(left, top, right, bottom);
        }
    }
}
