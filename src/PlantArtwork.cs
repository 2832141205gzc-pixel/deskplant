using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

internal sealed class PlantArtwork : IDisposable
{
    private readonly Dictionary<PlantSpecies, Sprites> plants = new Dictionary<PlantSpecies, Sprites>();
    private Sprites decorated;
    private PlantSpecies decoratedSpecies;
    private Decoration decoratedStyle;

    internal void Draw(Graphics graphics, PlantSpecies species, int stage, RectangleF area, bool animate, Decoration pot = null)
    {
        var sprites = GetSprites(species);
        if (pot != null && pot.Resource != null) sprites = GetDecoratedSprites(species, sprites, pot);
        if (!animate)
        {
            DrawFrame(graphics, sprites.Images[stage], sprites.SharedBounds, area);
            return;
        }

        if (sprites.Motion[stage] == null)
            sprites.Motion[stage] = new PlantSway(sprites.Images[stage], sprites.SharedBounds, sprites.FrameBounds[stage], species.FoliageBottom);
        sprites.Motion[stage].Draw(graphics, Fit(sprites.SharedBounds, area));
    }

    private Sprites GetDecoratedSprites(PlantSpecies species, Sprites original, Decoration pot)
    {
        if (decoratedSpecies == species && decoratedStyle == pot) return decorated;
        var pictures = new Bitmap[original.Images.Length];
        using (var stream = typeof(PlantArtwork).Assembly.GetManifestResourceStream(pot.Resource))
        using (var texture = new Bitmap(stream))
        {
            for (int i = 0; i < pictures.Length; i++)
            {
                var frame = original.Images[i];
                pictures[i] = CopyFrame(frame, new Rectangle(Point.Empty, frame.Size));
                PotFinish.Apply(pictures[i], original.FrameBounds[i], original.SharedBounds, species.FoliageBottom, texture,
                    species.Id == "pineapple");
            }
        }
        if (decorated != null) decorated.Dispose();
        decorated = new Sprites(pictures);
        decoratedSpecies = species;
        decoratedStyle = pot;
        return decorated;
    }

    internal Bitmap Preview(PlantSpecies species, int stage, Size size)
    {
        var image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            DrawPreview(graphics, species, stage, new RectangleF(0, 0, size.Width, size.Height));
        }
        return image;
    }

    internal void DrawPreview(Graphics graphics, PlantSpecies species, int stage, RectangleF area)
    {
        var sprites = GetSprites(species);
        DrawFrame(graphics, sprites.Images[stage], sprites.FrameBounds[stage], area);
    }

    internal PointF SoilPosition(PlantSpecies species, RectangleF area)
    {
        var destination = Fit(GetSprites(species).SharedBounds, area);
        return new PointF(destination.Left + destination.Width / 2,
                          destination.Top + destination.Height * (float)species.FoliageBottom + 11);
    }

    private static void DrawFrame(Graphics graphics, Bitmap image, Rectangle source, RectangleF area)
    {
        graphics.DrawImage(image, Fit(source, area), source, GraphicsUnit.Pixel);
    }

    private static RectangleF Fit(Rectangle source, RectangleF area)
    {
        var factor = Math.Min(area.Width / source.Width, area.Height / source.Height);
        var width = source.Width * factor;
        var height = source.Height * factor;
        return new RectangleF(area.X + (area.Width - width) / 2, area.Bottom - height, width, height);
    }

    private Sprites GetSprites(PlantSpecies species)
    {
        Sprites sprites;
        if (plants.TryGetValue(species, out sprites)) return sprites;

        var frames = new Bitmap[species.StageNames.Length];
        if (species.Resources.Length == 1)
        {
            using (var stream = typeof(PlantArtwork).Assembly.GetManifestResourceStream(species.Resources[0]))
            using (var sheet = new Bitmap(stream))
            {
                var width = sheet.Width / frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = CopyFrame(sheet, new Rectangle(i * width, 0, width, sheet.Height));
            }
        }
        else
        {
            for (int i = 0; i < frames.Length; i++)
            {
                using (var stream = typeof(PlantArtwork).Assembly.GetManifestResourceStream(species.Resources[i]))
                using (var image = new Bitmap(stream))
                    frames[i] = CopyFrame(image, new Rectangle(Point.Empty, image.Size));
            }
        }

        sprites = new Sprites(frames);
        plants.Add(species, sprites);
        return sprites;
    }

    private static Bitmap CopyFrame(Bitmap source, Rectangle crop)
    {
        var bitmap = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(source, new Rectangle(0, 0, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
        }
        return bitmap;
    }

    internal static Rectangle CleanAndFindBounds(Bitmap bitmap, int alphaMinimum = 9)
    {
        var full = new Rectangle(Point.Empty, bitmap.Size);
        var pixels = bitmap.LockBits(full, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[bitmap.Width * 4];
            int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < bitmap.Height; y++)
            {
                var address = IntPtr.Add(pixels.Scan0, y * pixels.Stride);
                Marshal.Copy(address, row, 0, row.Length);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var alpha = x * 4 + 3;
                    // Barely visible background speckles should not intercept desktop clicks.
                    if (row[alpha] < alphaMinimum) row[alpha] = 0;
                    if (row[alpha] < 28) continue;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
                Marshal.Copy(row, 0, address, row.Length);
            }
            if (maxX < minX) return full;
            return Rectangle.Intersect(full, Rectangle.FromLTRB(minX - 3, minY - 3, maxX + 4, maxY + 4));
        }
        finally { bitmap.UnlockBits(pixels); }
    }

    public void Dispose()
    {
        foreach (var sprites in plants.Values) sprites.Dispose();
        plants.Clear();
        if (decorated != null) decorated.Dispose();
    }

    private sealed class Sprites : IDisposable
    {
        internal readonly Bitmap[] Images;
        internal readonly Rectangle[] FrameBounds;
        internal readonly Rectangle SharedBounds;
        internal readonly PlantSway[] Motion;

        internal Sprites(Bitmap[] frames)
        {
            Images = frames;
            Motion = new PlantSway[frames.Length];
            FrameBounds = Array.ConvertAll(frames, frame => CleanAndFindBounds(frame));
            var bounds = FrameBounds[0];
            for (int i = 1; i < FrameBounds.Length; i++) bounds = Rectangle.Union(bounds, FrameBounds[i]);
            SharedBounds = bounds;
        }

        public void Dispose()
        {
            foreach (var motion in Motion) if (motion != null) motion.Dispose();
            foreach (var image in Images) image.Dispose();
        }
    }
}
