using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal sealed class WarehousePage : Panel
{
    private static readonly GrowingSpaceStyle Theme = GrowingSpaceStyle.Warehouse;
    private const int Columns = 4, ColumnPitch = 178, RowHeight = 149, CardWidth = 166;
    private readonly PlantState state;
    private readonly PlantArtwork plantArt;
    private readonly FruitPileArtwork fruitArt;
    private readonly Font nameFont, bodyFont;
    private readonly Panel inventory;
    private readonly Label summary;

    internal WarehousePage(PlantState state, PlantArtwork plantArt, FruitPileArtwork fruitArt, Font title, Font name, Font body, Action openShop)
    {
        this.state = state;
        this.plantArt = plantArt;
        this.fruitArt = fruitArt;
        nameFont = new Font(name.FontFamily, name.SizeInPoints * 96f / 72f, name.Style, GraphicsUnit.Pixel);
        bodyFont = new Font(body.FontFamily, body.SizeInPoints * 96f / 72f, body.Style, GraphicsUnit.Pixel);
        Size = new Size(766, 610);
        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        DoubleBuffered = true;
        var banner = new SpaceBanner("仓库", "Warehouse", Theme, title, body);
        Controls.Add(banner);
        summary = banner.Summary;
        var market = GardenWindow.MakeButton(this, new Rectangle(592, 156, 154, 33), "出售果实换金币", body, Theme);
        market.Click += delegate { openShop(); };
        inventory = new InventoryPanel { Bounds = new Rectangle(20, 198, 726, 395), AutoScroll = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AccessibleName = "仓库库存" };
        inventory.Paint += PaintInventory;
        inventory.Scroll += delegate { inventory.Invalidate(); };
        Controls.Add(inventory);
    }

    internal void RefreshInventory()
    {
        if (!Visible || !IsHandleCreated) return;
        var filled = PlantCatalog.All.Count(plant => state.CountOf(plant) > 0 || state.Plots.Any(plot => plot.Crop == plant));
        var fruitCount = PlantCatalog.All.Count(plant => state.FruitOf(plant) > 0);
        summary.Text = fruitCount + " 类果实  ·  " + filled + " 类植物";
        int height = PlantStart(fruitCount) + RowHeight * Rows(filled);
        inventory.AutoScrollMinSize = new Size(0, (int)(height * Math.Max(96, DesktopNative.GetDpiForWindow(Handle)) / 96f));
        inventory.Invalidate();
    }

    private void PaintInventory(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var dpi = g.DpiX / 96f;
        g.TranslateTransform(0, inventory.AutoScrollPosition.Y);
        g.ScaleTransform(dpi, dpi);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fruits = PlantCatalog.All.Where(plant => state.FruitOf(plant) > 0).ToArray();
        DrawText(g, "果实库存", nameFont, Theme.Ink, new RectangleF(2, 0, 250, 29), ContentAlignment.MiddleLeft);
        if (fruits.Length == 0) DrawEmpty(g, "去果园采摘成熟果实，收成会显示在这里。", 34, 136);
        for (int i = 0; i < fruits.Length; i++)
        {
            var crop = fruits[i];
            var quantity = state.FruitOf(crop);
            float x = ColumnPitch * (i % Columns), y = 34 + RowHeight * (i / Columns);
            DrawShelf(g, x, y);
            fruitArt.Draw(g, crop, quantity, new RectangleF(x + 11, y + 4, CardWidth - 22, 79));
            DrawText(g, crop.Name, nameFont, Theme.Ink, new RectangleF(x + 11, y + 96, CardWidth - 22, 20), ContentAlignment.MiddleLeft);
            DrawText(g, quantity + " 个", bodyFont, Theme.Muted, new RectangleF(x + 11, y + 115, CardWidth - 22, 19), ContentAlignment.MiddleLeft);
        }

        int plantTop = PlantStart(fruits.Length);
        DrawText(g, "种出的植物", nameFont, Theme.Ink, new RectangleF(2, plantTop - 34, 250, 29), ContentAlignment.MiddleLeft);
        int index = 0;
        foreach (var species in PlantCatalog.All)
        {
            var amount = species.IsCrop ? state.Plots.Count(plot => plot.Crop == species) : state.CountOf(species);
            if (amount == 0) continue;
            float x = ColumnPitch * (index % Columns), y = plantTop + RowHeight * (index / Columns);
            DrawShelf(g, x, y);
            plantArt.Draw(g, species, species.FinalStage, new RectangleF(x + 27, y + 4, CardWidth - 54, 75), false);
            DrawText(g, species.Name, nameFont, Theme.Ink, new RectangleF(x + 11, y + 96, CardWidth - 22, 20), ContentAlignment.MiddleLeft);
            DrawText(g, (species.IsCrop ? "果园 · " : "花园 · ") + amount + " 株", bodyFont, Theme.Muted,
                     new RectangleF(x + 11, y + 115, CardWidth - 22, 19), ContentAlignment.MiddleLeft);
            index++;
        }
        if (index == 0) DrawEmpty(g, "收纳到花园或移入果园的植物会显示在这里。", plantTop, 97);
    }

    private static int Rows(int count) { return Math.Max(1, (count + Columns - 1) / Columns); }
    private static int PlantStart(int fruitCount) { return 77 + RowHeight * Rows(fruitCount); }

    private static void DrawShelf(Graphics g, float x, float y)
    {
        var card = new RectangleF(x, y, CardWidth, 137);
        GrowingSpaceStyle.FillRounded(g, card, 10, Theme.Surface);
        using (var outline = GrowingSpaceStyle.Rounded(card, 10))
        using (var edge = new Pen(Theme.Edge)) g.DrawPath(edge, outline);
        var niche = new RectangleF(x + 6, y + 6, CardWidth - 12, 83);
        using (var outline = GrowingSpaceStyle.Rounded(niche, 6))
        using (var shade = new LinearGradientBrush(niche, Theme.SceneTop, Theme.SceneBottom, 90f)) g.FillPath(shade, outline);
        var wood = new RectangleF(x + 6, y + 79, CardWidth - 12, 10);
        using (var grain = new LinearGradientBrush(wood, Color.FromArgb(184, 134, 85), Color.FromArgb(140, 93, 57), 90f))
            g.FillRectangle(grain, wood);
        using (var light = new Pen(Color.FromArgb(216, 172, 124)))
            g.DrawLine(light, x + 7, y + 80, x + CardWidth - 7, y + 80);
    }

    private void DrawEmpty(Graphics g, string text, float y, float height)
    {
        var bounds = new RectangleF(0, y, 700, height);
        GrowingSpaceStyle.FillRounded(g, bounds, 10, Theme.Surface);
        DrawText(g, text, bodyFont, Theme.Muted, bounds, ContentAlignment.MiddleCenter);
    }

    private static void DrawText(Graphics g, string text, Font font, Color color, RectangleF bounds, ContentAlignment alignment)
    {
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat { LineAlignment = StringAlignment.Center,
            Alignment = alignment == ContentAlignment.MiddleCenter ? StringAlignment.Center : StringAlignment.Near })
            g.DrawString(text, font, brush, bounds, format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { nameFont.Dispose(); bodyFont.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class InventoryPanel : Panel
    {
        internal InventoryPanel() { DoubleBuffered = true; }
    }
}
