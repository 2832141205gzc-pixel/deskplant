using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

internal sealed class GardenWindow : Form
{
    internal const int JournalSection = 4, ShopSection = 5, TestSection = 6;
    private static readonly GrowingSpaceStyle Theme = GrowingSpaceStyle.Garden;
    private static readonly Color Green = Color.FromArgb(34, 81, 63);
    private static readonly Color Muted = Color.FromArgb(81, 100, 88);
    private readonly PlantState state;
    private readonly Label count, empty, current, overview;
    private readonly Button archive;
    private readonly Panel fieldView, seedView, shelf;
    private readonly PlantArtwork artwork;
    private readonly Action<PlantSpecies> uprootGarden;
    private readonly PlantSpecies[] ornamentals = PlantCatalog.All.Where(plant => !plant.IsCrop).ToArray();
    private readonly List<GardenPreview> previews = new List<GardenPreview>();
    private int[] plotted = new int[0];
    private readonly Button[] seeds = new Button[PlantCatalog.All.Length];
    private readonly Panel[] pages;
    private readonly Button[] navigation;
    private int sectionIndex;
    private readonly OrchardPage orchard;
    private readonly WarehousePage warehouse;
    private readonly RecyclePage recycle;
    private readonly PlantJournalPage journal;
    private readonly CoinShopPage shop;
    internal event Action DecorationChanged;
#if DESKPLANT_TEST
    private readonly TestToolsPage testTools;
    internal event Action TestChanged;
#endif
    private readonly CropArtwork cropArt;
    private readonly FruitPileArtwork fruitArt = new FruitPileArtwork();
    private readonly System.Collections.Generic.List<Image> pictures = new System.Collections.Generic.List<Image>();
    private readonly Timer timer = new Timer { Interval = 5000 };
    private readonly Timer motionTimer = new Timer { Interval = PlantSway.FrameInterval };
    private readonly ContextMenuStrip settingsMenu = new ContextMenuStrip();
    private readonly Font titleFont = new Font("Microsoft YaHei UI", 23, FontStyle.Bold);
    private readonly Font nameFont = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
    private readonly Font bodyFont = new Font("Microsoft YaHei UI", 9);
    private readonly Image gardenBed = GrowingSpaceStyle.LoadArt("GardenBed");
    private bool released;

    internal GardenWindow(PlantState state, PlantArtwork artwork, Action archivePlant, Action<PlantSpecies> sowPlant,
                          Action<bool> changeMotion, Action<OrchardPlot> harvest, Action<bool> setAuto,
                          Action<PlantSpecies> uprootGarden, Action<OrchardPlot> uprootPlot, Action<RecycledPlant> restore)
    {
        this.state = state;
        this.artwork = artwork;
        this.uprootGarden = uprootGarden;
        cropArt = new CropArtwork(fruitArt);
#if DESKPLANT_TEST
        Text = "我的种植空间 · 测试版";
#else
        Text = "我的种植空间";
#endif
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Canvas;
        ClientSize = new Size(766, 664);
        var strip = new Panel { Bounds = new Rectangle(0, 0, 766, 54), BackColor = GrowingSpaceStyle.Navigation,
                                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        Controls.Add(strip);
        AddLabel(strip, new Rectangle(20, 8, 137, 38), "DeskPlant", nameFont, GrowingSpaceStyle.NavigationInk);
        var deck = new Panel { Bounds = new Rectangle(0, 54, 766, 610), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        Controls.Add(deck);
#if DESKPLANT_TEST
        var sections = new[] { "花园", "果园", "仓库", "回收站", "图鉴", "商店", "测试工具" };
#else
        var sections = new[] { "花园", "果园", "仓库", "回收站", "图鉴", "商店" };
#endif
        pages = new Panel[sections.Length];
        navigation = new Button[sections.Length];
        for (int i = 0; i < sections.Length; i++)
        {
            var index = i;
            pages[i] = new Panel { Size = new Size(766, 610), Dock = DockStyle.Fill, BackColor = Theme.Canvas, Visible = i == 0 };
            deck.Controls.Add(pages[i]);
            var button = MakeButton(strip, new Rectangle(132 + 88 * i, 10, 82, 34), sections[i], nameFont);
            button.AccessibleRole = AccessibleRole.PageTab;
            button.Click += delegate { SelectSection(index); };
            navigation[i] = button;
        }
        UpdateNavigation();
        orchard = new OrchardPage(state, cropArt, titleFont, bodyFont, nameFont, harvest, setAuto, uprootPlot);
        warehouse = new WarehousePage(state, artwork, fruitArt, titleFont, nameFont, bodyFont, delegate { SelectSection(ShopSection); });
        recycle = new RecyclePage(state, artwork, bodyFont, nameFont, restore);
        journal = new PlantJournalPage(state, artwork, titleFont, nameFont, bodyFont);
        shop = new CoinShopPage(state, artwork, titleFont, nameFont, bodyFont);
        shop.Changed += delegate
        {
            warehouse.RefreshInventory();
            if (DecorationChanged != null) DecorationChanged();
        };
        pages[1].Controls.Add(orchard);
        pages[2].Controls.Add(warehouse);
        pages[3].Controls.Add(recycle);
        pages[JournalSection].Controls.Add(journal);
        pages[ShopSection].Controls.Add(shop);
#if DESKPLANT_TEST
        testTools = new TestToolsPage(state, bodyFont, nameFont, delegate { if (TestChanged != null) TestChanged(); });
        pages[TestSection].Controls.Add(testTools);
#endif
        fieldView = new Panel { Size = new Size(766, 610), Dock = DockStyle.Fill };
        seedView = new Panel { Size = new Size(766, 610), Dock = DockStyle.Fill, Visible = false };
        pages[0].Controls.Add(fieldView);
        pages[0].Controls.Add(seedView);

        var banner = new SpaceBanner("花园", "Garden", Theme, titleFont, bodyFont);
        fieldView.Controls.Add(banner);
        count = banner.Summary;
        overview = AddLabel(fieldView, new Rectangle(20, 158, 433, 30), "", bodyFont, Theme.Muted);
        var chooseSeeds = MakeButton(fieldView, new Rectangle(463, 156, 82, 34), "选种", bodyFont, Theme);
        chooseSeeds.Click += delegate { ShowSeeds(true); };
        var settings = MakeTonalButton(fieldView, new Rectangle(668, 156, 78, 34), "设置", bodyFont, Theme);
        settings.AccessibleName = "花园设置";
        var dynamicItem = new ToolStripMenuItem("动态效果", null, delegate { changeMotion(true); });
        var staticItem = new ToolStripMenuItem("静态效果", null, delegate { changeMotion(false); });
        settingsMenu.Items.AddRange(new ToolStripItem[] { dynamicItem, staticItem });
        settingsMenu.Opening += delegate
        {
            dynamicItem.Checked = state.MotionEnabled;
            staticItem.Checked = !state.MotionEnabled;
        };
        settings.Click += delegate { settingsMenu.Show(settings, new Point(settings.Width, settings.Height), ToolStripDropDownDirection.BelowLeft); };
        empty = AddLabel(fieldView, new Rectangle(20, 202, 726, 152),
            "花园还空着\n盆栽成熟后，选择「移入花园」。", nameFont, Theme.Muted, ContentAlignment.MiddleCenter);
        empty.BackColor = Theme.Surface;
        shelf = new Panel { Bounds = new Rectangle(20, 202, 726, 391), AutoScroll = true,
                            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        fieldView.Controls.Add(shelf);
        archive = MakeTonalButton(fieldView, new Rectangle(554, 156, 105, 34), "移入花园", bodyFont, Theme);
        archive.Click += delegate { archivePlant(); };

        var seedBanner = new SpaceBanner("选择下一株", "Garden", Theme, titleFont, bodyFont);
        seedView.Controls.Add(seedBanner);
        current = seedBanner.Summary;
        var back = MakeTonalButton(seedView, new Rectangle(624, 156, 122, 34), "返回花园", bodyFont, Theme);
        back.Click += delegate { ShowSeeds(false); };
        var seedShelf = new Panel { Bounds = new Rectangle(20, 202, 726, 391), AutoScroll = true,
                                 Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        seedView.Controls.Add(seedShelf);

        for (int i = 0; i < PlantCatalog.All.Length; i++)
        {
            var species = PlantCatalog.All[i];
            var card = new Panel { Bounds = new Rectangle(236 * (i % 3), 91 * (i / 3), 226, 79), BackColor = Theme.Surface };
            seedShelf.Controls.Add(card);
            AddPicture(card, artwork.Preview(species, 0, new Size(96, 116)), new Rectangle(5, 7, 58, 63));
            AddLabel(card, new Rectangle(68, 4, 148, 22), species.Name, nameFont, Theme.Ink);
            var stages = species.StageNames.Length + " 个阶段";
            AddLabel(card, new Rectangle(68, 25, 138, 19), species.IsCrop ? stages + " · 最多 " + species.FruitCapacity + " 颗" :
                stages, bodyFont, Theme.Muted);
            var seed = MakeButton(card, new Rectangle(69, 46, 91, 24), "种下", bodyFont, Theme);
            seed.Click += delegate { sowPlant(species); };
            seed.AccessibleName = "种下" + species.Name;
            seeds[i] = seed;
        }

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        timer.Tick += delegate { RefreshGarden(); };
        motionTimer.Tick += delegate { RefreshPreviews(); };
        RefreshGarden();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var screen = Screen.FromControl(Owner ?? this).WorkingArea;
        if (Height > screen.Height) Height = screen.Height;
        Location = new Point(screen.Left + Math.Max(0, (screen.Width - Width) / 2),
                             screen.Top + Math.Max(0, (screen.Height - Height) / 2));
        timer.Start();
        RefreshGarden();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (sectionIndex == JournalSection && (keyData == Keys.PageUp || keyData == Keys.PageDown) && !journal.DirectoryOpen)
        {
            journal.TurnPage(keyData == Keys.PageUp ? -1 : 1);
            return true;
        }
        if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab))
        {
            int offset = (keyData & Keys.Shift) == Keys.Shift ? -1 : 1;
            SelectSection((sectionIndex + pages.Length + offset) % pages.Length);
            navigation[sectionIndex].Focus();
            return true;
        }
        if (keyData != Keys.Escape) return base.ProcessCmdKey(ref message, keyData);
        Close();
        return true;
    }

    internal void RefreshGarden()
    {
        var reading = state.Read(DateTime.UtcNow);
        var counts = ornamentals.Select(state.CountOf).ToArray();
        var total = counts.Sum(amount => (long)amount);
        if (fieldView.Visible && IsHandleCreated && !plotted.SequenceEqual(counts)) BuildGardenPlots(counts);
        var canSow = reading.Species == null;
        for (int i = 0; i < seeds.Length; i++)
        {
            SetActionEnabled(seeds[i], canSow, Theme);
        }
        empty.Visible = total == 0;
        shelf.Visible = total > 0;
        count.Text = total + " 块地  ·  " + counts.Count(amount => amount > 0) + " 个品种";
        archive.Visible = reading.Mature;
        archive.Text = reading.Species != null && reading.Species.IsCrop ? "移入果园" : "移入花园";
        overview.Text = current.Text = reading.Species == null ? "选一个品种，开始种下一株。" :
            "桌面盆栽  ·  " + reading.Species.Name + "  ·  " + reading.StageName + "  " + reading.ProgressText;
        motionTimer.Enabled = fieldView.Visible && state.MotionEnabled && total > 0;
        RefreshPreviews();
        orchard.RefreshPlots();
        warehouse.RefreshInventory();
        RefreshRecycle();
        if (sectionIndex == JournalSection) journal.RefreshJournal();
        if (sectionIndex == ShopSection && shop.Visible) shop.RefreshShop();
#if DESKPLANT_TEST
        testTools.RefreshTools();
#endif
    }

    internal void RefreshRecycle()
    {
        var now = DateTime.UtcNow;
        int live = state.RecycleBin.Count(entry => !entry.IsExpired(now));
        navigation[3].Text = live == 0 ? "回收站" : "回收站 " + live;
        recycle.RefreshBin();
    }

    internal void SelectSection(int section, bool chooseSeed = false)
    {
        if (section < 0 || section >= pages.Length) return;
        if (sectionIndex != section)
        {
            pages[sectionIndex].Hide();
            sectionIndex = section;
            pages[sectionIndex].Show();
            UpdateNavigation();
        }
        if (section == 0) ShowSeeds(chooseSeed);
        else RefreshGarden();
    }

    private void UpdateNavigation()
    {
        for (int i = 0; i < navigation.Length; i++)
        {
            var button = navigation[i];
            bool selected = i == sectionIndex;
            button.BackColor = selected ? GrowingSpaceStyle.NavigationSelection : GrowingSpaceStyle.Navigation;
            button.ForeColor = selected ? GrowingSpaceStyle.Navigation : GrowingSpaceStyle.NavigationInk;
            button.FlatAppearance.MouseOverBackColor = selected ? GrowingSpaceStyle.NavigationSelection : Color.FromArgb(49, 75, 59);
            button.FlatAppearance.MouseDownBackColor = GrowingSpaceStyle.NavigationSelection;
            button.AccessibleDescription = selected ? "当前页面" : null;
        }
    }

    private void ShowSeeds(bool open)
    {
        fieldView.Visible = !open;
        seedView.Visible = open;
        RefreshGarden();
    }

    private void BuildGardenPlots(int[] counts)
    {
        var scroll = shelf.AutoScrollPosition;
        shelf.SuspendLayout();
        shelf.AutoScrollPosition = Point.Empty;
        while (shelf.Controls.Count != 0) shelf.Controls[0].Dispose();
        previews.Clear();
        plotted = counts;
        var scale = Math.Max(1, DesktopNative.GetDpiForWindow(Handle)) / 96f;
        var position = 0;
        for (int group = 0; group < ornamentals.Length; group++)
        {
            var species = ornamentals[group];
            for (int i = 0; i < counts[group]; i++)
            {
                var card = new SpacePlot(Theme) { Bounds = PlotLayout.Cell(scale, position++) };
                shelf.Controls.Add(card);
                AddLabel(card, PlotLayout.Bounds(scale, 13, 5, 144, 25), species.Name, nameFont, Theme.Ink).BackColor = Color.Transparent;
                AddLabel(card, PlotLayout.Bounds(scale, 13, 165, 144, 30), (i + 1) + " 号地", bodyFont, Theme.Muted).BackColor = Color.Transparent;
                var remove = MakeQuietButton(card, PlotLayout.Bounds(scale, 164, 6, 49, 23), "铲除", bodyFont);
                remove.AccessibleName = "铲除一株" + species.Name;
                remove.Click += delegate { uprootGarden(species); };
                var preview = new GardenPreview(state, artwork, species, gardenBed) {
                    Bounds = PlotLayout.Bounds(scale, 10, 34, 206, 126), BackColor = Color.Transparent
                };
                card.Controls.Add(preview);
                previews.Add(preview);
            }
        }
        shelf.AutoScrollMinSize = new Size(0, PlotLayout.ScrollHeight(scale, position));
        shelf.ResumeLayout();
        shelf.AutoScrollPosition = new Point(0, -scroll.Y);
    }

    internal void ShowHarvest(List<HarvestPick> picks)
    {
        orchard.ShowHarvest(picks);
        warehouse.RefreshInventory();
        if (sectionIndex == ShopSection && shop.Visible) shop.RefreshShop();
    }

    private void RefreshPreviews()
    {
        var viewport = shelf.ClientRectangle;
        foreach (var preview in previews)
            if (preview.Visible && viewport.IntersectsWith(preview.Parent.Bounds)) preview.Invalidate();
    }

    private void AddPicture(Control parent, Image image, Rectangle bounds)
    {
        pictures.Add(image);
        parent.Controls.Add(new PictureBox { Bounds = bounds, Image = image, SizeMode = PictureBoxSizeMode.Zoom });
    }

    internal static Label AddLabel(Control parent, Rectangle bounds, string text, Font font, Color color,
        ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        var label = new Label { Bounds = bounds, Text = text, Font = font, ForeColor = color, TextAlign = alignment };
        parent.Controls.Add(label);
        return label;
    }

    internal static Button MakeButton(Control parent, Rectangle bounds, string text, Font font, GrowingSpaceStyle theme = null)
    {
        var button = new Button { Bounds = bounds, Text = text, Font = font, BackColor = theme == null ? Green : theme.Action,
                                  ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        parent.Controls.Add(button);
        return button;
    }

    private static Button MakeTonalButton(Control parent, Rectangle bounds, string text, Font font, GrowingSpaceStyle theme)
    {
        var button = MakeButton(parent, bounds, text, font, theme);
        button.BackColor = theme.Edge;
        button.ForeColor = theme.Ink;
        return button;
    }

    internal static Button MakeQuietButton(Control parent, Rectangle bounds, string text, Font font)
    {
        var button = MakeButton(parent, bounds, text, font);
        button.BackColor = Color.FromArgb(242, 231, 218);
        button.ForeColor = Color.FromArgb(113, 68, 46);
        return button;
    }

    internal static void SetActionEnabled(Button button, bool enabled, GrowingSpaceStyle theme = null)
    {
        button.Enabled = enabled;
        var action = theme == null ? Green : theme.Action;
        var inactive = theme == null ? Color.FromArgb(229, 235, 225) : theme.Edge;
        button.BackColor = enabled ? action : inactive;
        button.ForeColor = enabled ? Color.White : (theme == null ? Muted : theme.Muted);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || released) return;
        released = true;
        timer.Dispose();
        motionTimer.Dispose();
        settingsMenu.Dispose();
        cropArt.Dispose();
        fruitArt.Dispose();
        gardenBed.Dispose();
        foreach (var picture in pictures) picture.Dispose();
        titleFont.Dispose();
        nameFont.Dispose();
        bodyFont.Dispose();
    }

    private sealed class GardenPreview : Control
    {
        private readonly PlantState state;
        private readonly PlantArtwork artwork;
        private readonly PlantSpecies species;
        private readonly Image bed;

        internal GardenPreview(PlantState state, PlantArtwork artwork, PlantSpecies species, Image bed)
        {
            this.state = state;
            this.artwork = artwork;
            this.species = species;
            this.bed = bed;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width <= 16 || ClientSize.Height <= 4) return;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var soil = new RectangleF(ClientSize.Width * .07f, ClientSize.Height * .64f, ClientSize.Width * .86f, ClientSize.Height * .34f);
            e.Graphics.DrawImage(bed, soil);
            var area = new RectangleF(ClientSize.Width * .16f, 4, ClientSize.Width * .68f, ClientSize.Height * .82f);
            artwork.Draw(e.Graphics, species, species.FinalStage, area, state.MotionEnabled);
        }
    }
}
