using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal sealed class CropArtwork : IDisposable
{
    private readonly Dictionary<PlantSpecies, Crop> crops = new Dictionary<PlantSpecies, Crop>();
    private readonly FruitPileArtwork fruitArt;
    private readonly Image orchardBed = GrowingSpaceStyle.LoadArt("OrchardBed");
    private AtlasLayout layout;

    internal CropArtwork(FruitPileArtwork fruitArt) { this.fruitArt = fruitArt; }

    internal void DrawPlot(Graphics g, PlantSpecies species, RectangleF area, FruitSchedule current, FruitSchedule picked, double pick)
    {
        var crop = Get(species);
        var transform = g.Save();
        float scale = Math.Min(area.Width / 256f, area.Height / 201f);
        g.TranslateTransform(area.X + (area.Width - 256 * scale) / 2, area.Y + (area.Height - 201 * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.DrawImage(orchardBed, new RectangleF(20, 143, 216, 57));

        DrawCrown(g, crop);
        var visible = pick >= 0 && pick < .44 ? picked : current;
        var greenSize = crop.FruitSize * .78f;
        if (visible.Ripe < species.FruitCapacity)
            DrawBranchFruit(g, species, crop, crop.Slots[visible.NextSlot], greenSize, true);
        for (int i = 0; i < visible.Ripe; i++)
            DrawBranchFruit(g, species, crop, crop.Slots[visible.RipeSlot(i, species.FruitCapacity)], crop.FruitSize, false);

        if (pick >= 0 && picked.Ripe > 0) DrawPick(g, species, crop, picked, pick);
        g.Restore(transform);
    }

    private void DrawBranchFruit(Graphics g, PlantSpecies species, Crop crop, PointF center, float size, bool green)
    {
        var destination = new RectangleF(center.X - size / 2, center.Y - size / 2, size, size);
        if (crop.Harvest != null) DrawPart(g, green ? crop.Green : crop.Ripe, destination);
        else fruitArt.DrawSingle(g, species, destination, green);
    }

    private static void DrawPart(Graphics g, Part part, RectangleF destination)
    {
        float scale = Math.Min(destination.Width / part.Bounds.Width, destination.Height / part.Bounds.Height);
        var size = new SizeF(part.Bounds.Width * scale, part.Bounds.Height * scale);
        var target = new RectangleF(destination.X + (destination.Width - size.Width) / 2,
                                    destination.Y + (destination.Height - size.Height) / 2, size.Width, size.Height);
        g.DrawImage(part.Image, target, part.Bounds, GraphicsUnit.Pixel);
    }

    private void DrawPick(Graphics g, PlantSpecies species, Crop crop, FruitSchedule picked, double progress)
    {
        var target = crop.Slots[picked.RipeSlot(0, species.FruitCapacity)];
        target.Y += crop.FruitSize * (crop.PickHeight - .5f);
        float away = (float)(progress < .44 ? 1 - Ease(progress / .44) : Ease((progress - .44) / .56));
        var x = target.X + 65 * away;
        var y = target.Y - 51 * away;
        if (progress >= .44)
        {
            var gather = (float)Ease(Math.Min(1, (progress - .44) / .22));
            var size = crop.FruitSize + (Math.Min(crop.FruitSize, 30) - crop.FruitSize) * gather;
            for (int i = 0; i < picked.Ripe; i++)
            {
                var slot = crop.Slots[picked.RipeSlot(i, species.FruitCapacity)];
                var startX = slot.X + x - target.X;
                var startY = slot.Y + y - target.Y;
                var carriedX = x - 21 + i % 3 * 12;
                var carriedY = y - 16 - i / 3 * 12;
                var centerX = startX + (carriedX - startX) * gather;
                var centerY = startY + (carriedY - startY) * gather;
                var destination = new RectangleF(centerX - size / 2, centerY - size / 2, size, size);
                if (crop.Harvest != null) DrawPart(g, crop.Harvest, destination);
                else fruitArt.DrawSingle(g, species, destination, false);
            }
        }
        var position = g.Save();
        g.TranslateTransform(x, y);
        g.RotateTransform(28);
        using (var shape = new GraphicsPath())
        using (var trim = new Pen(Color.FromArgb(112, 81, 48), 1.8f) { LineJoin = LineJoin.Round })
        using (var leather = new SolidBrush(Color.FromArgb(255, 225, 164)))
        using (var cuff = new SolidBrush(Color.FromArgb(122, 157, 111)))
        {
            shape.AddBezier(-10, 10, -19, 9, -23, 5, -21, 0);
            shape.AddBezier(-21, 0, -20, -4, -15, -3, -11, -1);
            shape.AddLine(-11, -1, -12, -18);
            shape.AddBezier(-12, -18, -12, -24, -5, -24, -5, -18);
            shape.AddLine(-5, -18, -5, -12);
            shape.AddBezier(-5, -12, -2, -16, 3, -15, 4, -10);
            shape.AddBezier(4, -10, 8, -14, 12, -11, 12, -6);
            shape.AddBezier(12, -6, 18, -8, 20, -1, 18, 7);
            shape.AddBezier(18, 7, 18, 14, 14, 19, 12, 22);
            shape.AddLine(12, 22, -8, 22);
            shape.CloseFigure();
            g.FillPath(leather, shape);
            g.DrawPath(trim, shape);
            g.FillRectangle(cuff, -10, 18, 24, 9);
            g.DrawRectangle(trim, -10, 18, 24, 9);
        }
        g.Restore(position);
    }

    private static double Ease(double x) { return x * x * (3 - 2 * x); }

    private static void DrawCrown(Graphics g, Crop crop)
    {
        var part = crop.Crown;
        var layer = crop.CrownLayer;
        float width = layer.width, height = width * part.Image.Height / part.Image.Width;
        g.DrawImage(part.Image, new RectangleF((float)(layer.at[0] - width * part.Pivot[0]),
            (float)(layer.at[1] - height * part.Pivot[1]), width, height));
    }

    private Crop Get(PlantSpecies species)
    {
        Crop crop;
        if (crops.TryGetValue(species, out crop)) return crop;
        if (layout == null)
            using (var stream = typeof(CropArtwork).Assembly.GetManifestResourceStream("DeskPlant.Crop.Layers"))
            using (var reader = new StreamReader(stream)) layout = new JavaScriptSerializer().Deserialize<AtlasLayout>(reader.ReadToEnd());

        var data = layout.plants.Single(plant => plant.id == species.Id);
        var crown = data.stages[data.stages.Length - 1].layers[0];
        float fruitSize;
        var slots = BranchSlots(species, out fruitSize);
        crop = new Crop(crown, slots, fruitSize, (float)(data.pickHeight ?? .5));
        using (var stream = typeof(CropArtwork).Assembly.GetManifestResourceStream("DeskPlant.Crop." + species.Id + ".Parts"))
        using (var sheet = new Bitmap(stream))
        {
            crop.Crown = ReadPart(sheet, data, crown.part);
            if (data.parts.ContainsKey("harvest"))
            {
                crop.Ripe = ReadPart(sheet, data, "ripe");
                crop.Green = ReadPart(sheet, data, "green");
                crop.Harvest = ReadPart(sheet, data, "harvest");
            }
        }
        crops.Add(species, crop);
        return crop;
    }

    private static Part ReadPart(Bitmap sheet, SpeciesLayout data, string key)
    {
        var part = data.parts[key];
        var rect = new Rectangle(part.crop[0], part.crop[1], part.crop[2], part.crop[3]);
        var bitmap = sheet.Clone(rect, PixelFormat.Format32bppArgb);
        return new Part(bitmap, part.pivot, PlantArtwork.CleanAndFindBounds(bitmap, data.alphaMin));
    }

    // These slots sit on the mature crown in the 256 × 201 orchard illustration.
    private static PointF[] BranchSlots(PlantSpecies species, out float size)
    {
        switch (species.Id)
        {
            case "passion-fruit":
                size = 31;
                return new[] { new PointF(166, 132), new PointF(107, 147), new PointF(139, 117) };
            case "pineapple":
                size = 77;
                return new[] { new PointF(128, 111), new PointF(92, 121), new PointF(163, 118) };
            case "blueberry":
                size = 25;
                return new[] { new PointF(108, 111), new PointF(173, 92), new PointF(130, 121), new PointF(89, 97), new PointF(158, 132) };
            case "strawberry":
                size = 29;
                return new[] { new PointF(112, 149), new PointF(146, 148), new PointF(83, 156), new PointF(173, 154) };
            case "kumquat":
                size = 26;
                return new[] { new PointF(103, 110), new PointF(169, 94), new PointF(73, 119), new PointF(142, 129) };
            case "cherry-tomato":
                size = 26;
                return new[] { new PointF(136, 94), new PointF(116, 123), new PointF(158, 121), new PointF(102, 97), new PointF(136, 148) };
            default:
                throw new ArgumentOutOfRangeException("species");
        }
    }

    public void Dispose()
    {
        orchardBed.Dispose();
        foreach (var crop in crops.Values)
        {
            crop.Crown.Image.Dispose();
            if (crop.Harvest == null) continue;
            crop.Harvest.Image.Dispose();
            crop.Ripe.Image.Dispose();
            crop.Green.Image.Dispose();
        }
        crops.Clear();
    }

    private sealed class Crop
    {
        internal readonly Layer CrownLayer;
        internal readonly PointF[] Slots;
        internal readonly float FruitSize, PickHeight;
        internal Part Crown, Ripe, Green, Harvest;
        internal Crop(Layer crown, PointF[] slots, float fruitSize, float pickHeight)
        {
            CrownLayer = crown;
            Slots = slots;
            FruitSize = fruitSize;
            PickHeight = pickHeight;
        }
    }

    private sealed class Part
    {
        internal readonly Bitmap Image;
        internal readonly double[] Pivot;
        internal readonly Rectangle Bounds;
        internal Part(Bitmap image, double[] pivot, Rectangle bounds) { Image = image; Pivot = pivot; Bounds = bounds; }
    }

    // Property names match the embedded atlas JSON.
    public sealed class AtlasLayout { public SpeciesLayout[] plants { get; set; } }
    public sealed class SpeciesLayout
    {
        public string id { get; set; }
        public int alphaMin { get; set; }
        public double? pickHeight { get; set; }
        public Dictionary<string, PartLayout> parts { get; set; }
        public StageLayout[] stages { get; set; }
    }
    public sealed class PartLayout { public int[] crop { get; set; } public double[] pivot { get; set; } }
    public sealed class StageLayout { public Layer[] layers { get; set; } }
    public sealed class Layer { public string part { get; set; } public double[] at { get; set; } public int width { get; set; } }
}
