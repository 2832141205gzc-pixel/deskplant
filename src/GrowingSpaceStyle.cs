using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class GrowingSpaceStyle
{
    internal static readonly Color Navigation = Color.FromArgb(31, 53, 43);
    internal static readonly Color NavigationInk = Color.FromArgb(233, 240, 224);
    internal static readonly Color NavigationSelection = Color.FromArgb(215, 229, 197);

    internal static readonly GrowingSpaceStyle Garden = new GrowingSpaceStyle(0xEEF0E4, 0xFFFCF2, 0xD4DDC6, 0x294733, 0x4D6251, 0x3D684D, 0xEAF0D9, 0xDCE7CC);
    internal static readonly GrowingSpaceStyle Orchard = new GrowingSpaceStyle(0xF3EEE0, 0xFFFAEF, 0xE0D6BD, 0x3F4930, 0x665E49, 0x55643C, 0xF5ECD3, 0xEDE1BC);
    internal static readonly GrowingSpaceStyle Warehouse = new GrowingSpaceStyle(0xF3E9DA, 0xFFF8EB, 0xDECCB1, 0x563D2B, 0x735A45, 0x8B5739, 0xFAECD7, 0xEDD5B8);

    internal readonly Color Canvas, Surface, Edge, Ink, Muted, Action, SceneTop, SceneBottom;

    private GrowingSpaceStyle(int canvas, int surface, int edge, int ink, int muted, int action, int sceneTop, int sceneBottom)
    {
        Canvas = ColorOf(canvas);
        Surface = ColorOf(surface);
        Edge = ColorOf(edge);
        Ink = ColorOf(ink);
        Muted = ColorOf(muted);
        Action = ColorOf(action);
        SceneTop = ColorOf(sceneTop);
        SceneBottom = ColorOf(sceneBottom);
    }

    private static Color ColorOf(int rgb) { return Color.FromArgb(unchecked((int)0xFF000000) | rgb); }

    internal static Image LoadArt(string resource)
    {
        using (var stream = typeof(GrowingSpaceStyle).Assembly.GetManifestResourceStream("DeskPlant.Space." + resource))
        using (var original = Image.FromStream(stream)) return new Bitmap(original);
    }

    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color color)
    {
        using (var outline = Rounded(bounds, radius))
        using (var brush = new SolidBrush(color)) graphics.FillPath(brush, outline);
    }
}

internal sealed class SpaceBanner : Panel
{
    private readonly Image scene;
    internal readonly Label Summary;

    internal SpaceBanner(string title, string resource, GrowingSpaceStyle style, Font heading, Font body)
    {
        scene = GrowingSpaceStyle.LoadArt(resource);
        Bounds = new Rectangle(20, 16, 726, 132);
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        BackColor = style.Canvas;
        DoubleBuffered = true;
        var label = GardenWindow.AddLabel(this, new Rectangle(23, 25, 325, 46), title, heading, Color.White);
        label.BackColor = Color.Transparent;
        Summary = GardenWindow.AddLabel(this, new Rectangle(25, 76, 341, 26), "", body, Color.FromArgb(244, 243, 224));
        Summary.BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
        var graphics = e.Graphics;
        var previous = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var surface = new RectangleF(0, 0, ClientSize.Width, ClientSize.Height);
        using (var outline = GrowingSpaceStyle.Rounded(surface, 13 * graphics.DpiX / 96f))
        {
            // A textured scene is kept in the banner. The title stays legible over the shaded side.
            graphics.SetClip(outline, CombineMode.Intersect);
            graphics.DrawImage(scene, surface);
            var shadeBounds = new RectangleF(0, 0, surface.Width * .64f, surface.Height);
            using (var shade = new LinearGradientBrush(shadeBounds, Color.FromArgb(150, 19, 29, 22), Color.FromArgb(0, 19, 29, 22), 0f))
                graphics.FillRectangle(shade, shadeBounds);
        }
        graphics.Restore(previous);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) scene.Dispose();
        base.Dispose(disposing);
    }
}

internal class SpacePlot : Panel
{
    private readonly GrowingSpaceStyle style;
    private bool ready;

    internal SpacePlot(GrowingSpaceStyle style)
    {
        this.style = style;
        BackColor = style.Canvas;
        DoubleBuffered = true;
    }

    internal bool Ready
    {
        set
        {
            if (ready == value) return;
            ready = value;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        var graphics = e.Graphics;
        var dpi = graphics.DpiX / 96f;
        if (ClientSize.Width < 24 * dpi || ClientSize.Height < 44 * dpi) return;
        var previous = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(dpi, dpi, ClientSize.Width - 2 * dpi, ClientSize.Height - 5 * dpi);
        var shadow = bounds;
        shadow.Offset(0, 3 * dpi);
        GrowingSpaceStyle.FillRounded(graphics, shadow, 11 * dpi, Color.FromArgb(93, style.Edge));
        using (var outline = GrowingSpaceStyle.Rounded(bounds, 11 * dpi))
        using (var fill = new SolidBrush(style.Surface))
        using (var edge = new Pen(ready ? Color.FromArgb(175, 132, 67) : style.Edge, dpi))
        {
            graphics.FillPath(fill, outline);
            graphics.DrawPath(edge, outline);
        }
        var scene = new RectangleF(8 * dpi, 33 * dpi, ClientSize.Width - 16 * dpi, 128 * dpi);
        using (var outline = GrowingSpaceStyle.Rounded(scene, 7 * dpi))
        using (var gradient = new LinearGradientBrush(scene, style.SceneTop, style.SceneBottom, 90f))
            graphics.FillPath(gradient, outline);
        graphics.Restore(previous);
    }
}
