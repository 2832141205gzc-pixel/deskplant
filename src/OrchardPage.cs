using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class OrchardPage : Panel
{
    private static readonly GrowingSpaceStyle Theme = GrowingSpaceStyle.Orchard;
    private readonly PlantState state;
    private readonly CropArtwork artwork;
    private readonly Action<OrchardPlot> harvest, uproot;
    private readonly Font bodyFont, nameFont;
    private readonly Label summary, feedback, empty;
    private readonly CheckBox automatic;
    private readonly Button pickAll;
    private readonly Panel plots;
    private readonly List<PlotTile> tiles = new List<PlotTile>();
    private readonly Timer timer = new Timer { Interval = 1000 };
    private readonly Timer animation = new Timer { Interval = 32 };
    private OrchardPlot[] plotted = new OrchardPlot[0];
    private bool syncingAuto;
    private DateTime feedbackUntil;

    internal OrchardPage(PlantState state, CropArtwork artwork, Font title, Font body, Font name, Action<OrchardPlot> harvest,
                         Action<bool> setAuto, Action<OrchardPlot> uproot)
    {
        this.state = state;
        this.artwork = artwork;
        this.harvest = harvest;
        this.uproot = uproot;
        bodyFont = body;
        nameFont = name;
        Size = new Size(766, 610);
        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        var banner = new SpaceBanner("果园", "Orchard", Theme, title, body);
        Controls.Add(banner);
        summary = banner.Summary;
        feedback = GardenWindow.AddLabel(this, new Rectangle(20, 157, 428, 32), "", body, Theme.Muted);
        feedback.AutoEllipsis = true;
        automatic = new CheckBox { Bounds = new Rectangle(459, 158, 153, 30), Text = "长满自动采摘", Font = body, ForeColor = Theme.Ink };
        Controls.Add(automatic);
        automatic.CheckedChanged += delegate { if (!syncingAuto) setAuto(automatic.Checked); };
        pickAll = GardenWindow.MakeButton(this, new Rectangle(619, 156, 127, 34), "全部采摘", body, Theme);
        pickAll.Click += delegate { harvest(null); };
        empty = GardenWindow.AddLabel(this, new Rectangle(20, 202, 726, 152),
            "果园还空着\n作物结果后，选择「移入果园」。", name, Theme.Muted, ContentAlignment.MiddleCenter);
        empty.BackColor = Theme.Surface;
        plots = new Panel { Bounds = new Rectangle(20, 202, 726, 391), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                            AutoScroll = true };
        Controls.Add(plots);
        timer.Tick += delegate { RefreshPlots(); };
        animation.Tick += delegate
        {
            if (!Visible) { animation.Stop(); return; }
            var playing = false;
            foreach (var tile in tiles) if (tile.RefreshAnimation()) playing = true;
            if (!playing) animation.Stop();
        };
        VisibleChanged += delegate
        {
            timer.Enabled = Visible;
            if (Visible) RefreshPlots();
            else
            {
                animation.Stop();
                foreach (var tile in tiles) tile.ResetAnimation();
            }
        };
    }

    internal void RefreshPlots()
    {
        if (Visible && !plotted.SequenceEqual(state.Plots)) BuildPlots();
        var now = DateTime.UtcNow;
        int ready = state.Plots.Count(plot => plot.Read(now).Ripe > 0);
        summary.Text = state.Plots.Count + " 块地  ·  " + ready + " 株可采摘";
        syncingAuto = true;
        automatic.Checked = state.AutoHarvestEnabled;
        syncingAuto = false;
        GardenWindow.SetActionEnabled(pickAll, ready > 0, Theme);
        empty.Visible = state.Plots.Count == 0;
        plots.Visible = state.Plots.Count != 0;
        if (feedbackUntil <= now)
            feedback.Text = state.Plots.Count == 0 ? "" : "果实逐颗成熟，长满后暂停。";
        foreach (var tile in tiles) tile.RefreshStatus(now);
    }

    internal void ShowHarvest(List<HarvestPick> picks)
    {
        if (picks.Count == 0) return;
        RefreshPlots();
        var totals = picks.GroupBy(pick => pick.Plot.Crop).Select(group => group.Key.Name + " +" + group.Sum(item => item.Quantity));
        feedback.Text = "已入库  ·  " + string.Join("  /  ", totals);
        feedbackUntil = DateTime.UtcNow.AddSeconds(5);
        if (Visible && state.MotionEnabled)
        {
            var picked = picks.ToDictionary(pick => pick.Plot);
            foreach (var tile in tiles)
            {
                HarvestPick harvest;
                if (picked.TryGetValue(tile.Plot, out harvest)) tile.Play(harvest.Picked);
            }
            animation.Start();
        }
    }

    private void BuildPlots()
    {
        plotted = state.Plots.ToArray();
        plots.SuspendLayout();
        var scroll = plots.AutoScrollPosition;
        plots.AutoScrollPosition = Point.Empty;
        while (plots.Controls.Count != 0) plots.Controls[0].Dispose();
        tiles.Clear();
        float scale = IsHandleCreated ? Math.Max(1, DesktopNative.GetDpiForWindow(Handle)) / 96f : 1;
        var position = 0;
        foreach (var group in plotted.GroupBy(plot => plot.Crop).OrderBy(group => Array.IndexOf(PlantCatalog.All, group.Key)))
        {
            var plants = group.ToArray();
            for (int i = 0; i < plants.Length; i++)
            {
                var tile = new PlotTile(plants[i], i + 1, artwork, bodyFont, nameFont, scale, harvest, uproot) {
                    Bounds = PlotLayout.Cell(scale, position)
                };
                plots.Controls.Add(tile);
                tiles.Add(tile);
                position++;
            }
        }
        plots.AutoScrollMinSize = new Size(0, PlotLayout.ScrollHeight(scale, position));
        plots.ResumeLayout();
        plots.AutoScrollPosition = new Point(0, -scroll.Y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); animation.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class PlotTile : SpacePlot
    {
        private readonly CropArtwork artwork;
        private readonly PictureBox illustration;
        private readonly Label status;
        private readonly Button pick;
        private readonly Stopwatch effect = new Stopwatch();
        private FruitSchedule current, picked;
        internal readonly OrchardPlot Plot;

        internal PlotTile(OrchardPlot plot, int number, CropArtwork artwork, Font font, Font name, float dpi,
                          Action<OrchardPlot> harvest, Action<OrchardPlot> uproot)
            : base(Theme)
        {
            Plot = plot;
            this.artwork = artwork;
            var title = GardenWindow.AddLabel(this, PlotLayout.Bounds(dpi, 13, 5, 146, 25), plot.Crop.Name + " · " + number + " 号地", name, Theme.Ink);
            title.BackColor = Color.Transparent;
            title.AutoEllipsis = true;
            var remove = GardenWindow.MakeQuietButton(this, PlotLayout.Bounds(dpi, 164, 6, 49, 23), "铲除", font);
            remove.AccessibleName = "铲除" + plot.Crop.Name + "第" + number + "块地";
            remove.Click += delegate { uproot(plot); };
            illustration = new PictureBox { Bounds = PlotLayout.Bounds(dpi, 10, 34, 206, 126), TabStop = false, BackColor = Color.Transparent };
            illustration.Paint += PaintCrop;
            Controls.Add(illustration);
            status = GardenWindow.AddLabel(this, PlotLayout.Bounds(dpi, 13, 161, 122, 36), "", font, Theme.Muted);
            status.BackColor = Color.Transparent;
            pick = GardenWindow.MakeButton(this, PlotLayout.Bounds(dpi, 148, 165, 65, 30), "采摘", font, Theme);
            pick.AccessibleName = "采摘" + plot.Crop.Name + "第" + number + "块地";
            pick.Click += delegate { harvest(plot); };
        }

        internal void RefreshStatus(DateTime now)
        {
            var reading = Plot.Read(now);
            if (reading.Ripe != current.Ripe || reading.NextSlot != current.NextSlot) illustration.Invalidate();
            current = reading;
            Ready = current.Ripe > 0;
            GardenWindow.SetActionEnabled(pick, current.Ripe > 0, Theme);
            status.ForeColor = current.Ripe > 0 ? Color.FromArgb(117, 76, 37) : Theme.Muted;
            var capacity = Plot.Crop.FruitCapacity;
            status.Text = current.Ripe == capacity ? "已长满 · " + capacity + "/" + capacity :
                "熟果 " + current.Ripe + "/" + capacity + "\n下一颗 " +
                TimeSpan.FromSeconds(Math.Ceiling((current.NextAt - now).TotalSeconds)).ToString(@"hh\:mm\:ss");
        }

        internal void Play(FruitSchedule harvest) { picked = harvest; effect.Restart(); illustration.Invalidate(); }
        internal void ResetAnimation() { effect.Reset(); illustration.Invalidate(); }
        internal bool RefreshAnimation()
        {
            if (!effect.IsRunning) return false;
            if (effect.ElapsedMilliseconds >= 780) effect.Reset();
            illustration.Invalidate();
            return effect.IsRunning;
        }

        private void PaintCrop(object sender, PaintEventArgs e)
        {
            double progress = effect.IsRunning ? Math.Min(1, effect.ElapsedMilliseconds / 780.0) : -1;
            artwork.DrawPlot(e.Graphics, Plot.Crop, illustration.ClientRectangle, current, picked, progress);
        }
    }
}
