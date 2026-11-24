using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

internal sealed class PlantJournalPage : Panel
{
    private static readonly Color Ink = Color.FromArgb(44, 67, 49);
    private static readonly Color Muted = Color.FromArgb(101, 91, 70);
    private static readonly Color Gold = Color.FromArgb(157, 115, 61);
    private static readonly Color Paper = Color.FromArgb(250, 243, 224);
    private static readonly Color Sage = Color.FromArgb(222, 230, 207);
    private static readonly int StageTotal = PlantCatalog.All.Sum(plant => plant.StageNames.Length);

    private readonly PlantState state;
    private readonly Font speciesFont = new Font("SimSun", 28, FontStyle.Bold);
    private readonly Font latinFont = new Font("Georgia", 12, FontStyle.Italic);
    private readonly Font paragraphFont = new Font("Microsoft YaHei UI", 11);
    private readonly ComboBox contents;
    private readonly Button previous, next;
    private readonly Button[] stages = new Button[PlantCatalog.All.Max(plant => plant.StageNames.Length)];
    private readonly Label summary, folio, category, plantName, scientificName, introduction, growingNote, stageName, stageStatus, records;
    private readonly PlantPlate plate;
    private PlantSpecies plant;
    private int stage, recorded;

    internal PlantJournalPage(PlantState state, PlantArtwork artwork, Font title, Font name, Font body)
    {
        this.state = state;
        Size = new Size(766, 610);
        Dock = DockStyle.Fill;
        BackColor = GrowingSpaceStyle.Garden.Canvas;
        AutoScroll = true;
        AutoScrollMargin = new Size(0, 10);

        GardenWindow.AddLabel(this, new Rectangle(20, 13, 177, 44), "植物图鉴", title, Ink);
        summary = GardenWindow.AddLabel(this, new Rectangle(202, 23, 272, 28), "", body, Muted);
        GardenWindow.AddLabel(this, new Rectangle(487, 23, 47, 28), "目录", name, Ink);
        contents = new ComboBox { Bounds = new Rectangle(540, 23, 205, 29), Font = paragraphFont,
            ForeColor = Ink, BackColor = Paper, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "植物目录",
            IntegralHeight = false, MaxDropDownItems = 12, TabIndex = 0 };
        contents.Items.AddRange(PlantCatalog.All.Select(item => (object)item.Name).ToArray());
        Controls.Add(contents);

        var book = new BookSpread { Bounds = new Rectangle(20, 69, 726, 474), TabIndex = 1 };
        Controls.Add(book);
        category = BookLabel(book, 36, 26, 292, 27, name, Gold, ContentAlignment.MiddleCenter);
        plate = new PlantPlate(artwork) { Bounds = new Rectangle(36, 62, 292, 303) };
        book.Controls.Add(plate);
        stageName = BookLabel(book, 36, 373, 292, 32, name, Ink, ContentAlignment.MiddleCenter);
        stageStatus = BookLabel(book, 36, 406, 292, 27, body, Muted, ContentAlignment.MiddleCenter);

        plantName = BookLabel(book, 396, 39, 295, 48, speciesFont, Ink);
        scientificName = BookLabel(book, 397, 88, 292, 27, latinFont, Gold);
        introduction = BookLabel(book, 397, 147, 291, 104, paragraphFont, Ink, ContentAlignment.TopLeft);
        introduction.Padding = new Padding(0, 1, 0, 0);
        introduction.UseCompatibleTextRendering = true;

        BookLabel(book, 397, 275, 115, 28, name, Ink).Text = "种植记录";
        records = BookLabel(book, 513, 275, 175, 28, body, Muted, ContentAlignment.MiddleRight);
        for (int i = 0; i < stages.Length; i++)
        {
            var index = i;
            var button = GardenWindow.MakeButton(book, new Rectangle(397 + 74 * i, 314, 68, 38), "", body);
            button.TabIndex = i;
            button.Click += delegate { stage = index; ShowStage(); };
            stages[i] = button;
        }
        growingNote = BookLabel(book, 405, 395, 274, 42, body, Ink, ContentAlignment.MiddleCenter);

        previous = MakeTurnButton(new Rectangle(178, 554, 117, 35), "‹  上一页", 2);
        next = MakeTurnButton(new Rectangle(471, 554, 117, 35), "下一页  ›", 3);
        folio = GardenWindow.AddLabel(this, new Rectangle(308, 554, 150, 35), "", latinFont, Ink, ContentAlignment.MiddleCenter);
        previous.Click += delegate { TurnPage(-1); };
        next.Click += delegate { TurnPage(1); };
        contents.SelectedIndexChanged += delegate
        {
            if (contents.SelectedIndex >= 0) ShowSpecies(contents.SelectedIndex);
        };
        contents.SelectedIndex = 0;
    }

    internal void RefreshJournal()
    {
        if (!Visible) return;
        state.RecordGrowth(DateTime.UtcNow);
        var knownPlants = 0;
        var knownStages = 0;
        foreach (var item in PlantCatalog.All)
        {
            var count = state.JournalStage(item) + 1;
            if (count > 0) knownPlants++;
            knownStages += count;
        }
        summary.Text = "已记录 " + knownPlants + "/" + PlantCatalog.All.Length + " 种  ·  " + knownStages + "/" + StageTotal + " 个阶段";
        if (recorded == state.JournalStage(plant)) return;
        recorded = state.JournalStage(plant);
        ShowStage();
    }

    internal bool DirectoryOpen { get { return contents.DroppedDown; } }

    internal void TurnPage(int direction)
    {
        var index = contents.SelectedIndex + direction;
        if (index >= 0 && index < PlantCatalog.All.Length) contents.SelectedIndex = index;
    }

    private void ShowSpecies(int index)
    {
        plant = PlantCatalog.All[index];
        recorded = state.JournalStage(plant);
        stage = Math.Max(0, recorded);
        category.Text = plant.IsCrop ? "结果作物" : "观赏植物";
        plantName.Text = plant.Name;
        scientificName.Text = plant.ScientificName;
        introduction.Text = plant.Introduction;
        growingNote.Text = plant.IsCrop ? "结果后移入果园  ·  每株最多 " + plant.FruitCapacity + " 颗" : "成熟后移入花园";
        folio.Text = (index + 1).ToString("00") + "  /  " + PlantCatalog.All.Length.ToString("00");
        SetTurnEnabled(previous, index > 0);
        SetTurnEnabled(next, index < PlantCatalog.All.Length - 1);
        ShowStage();
    }

    private void ShowStage()
    {
        var unlocked = stage <= recorded;
        plate.ShowStage(plant, stage, unlocked);
        stageName.Text = plant.StageNames[stage] + "阶段";
        stageStatus.Text = unlocked ? "已记录" : "种到这个阶段后解锁图版";
        records.Text = "已记录 " + (recorded + 1) + "/" + plant.StageNames.Length + " 个阶段";
        for (int i = 0; i < stages.Length; i++)
        {
            var button = stages[i];
            button.Visible = i <= plant.FinalStage;
            if (i > plant.FinalStage) continue;
            button.Text = plant.StageNames[i];
            button.BackColor = i == stage ? Ink : i <= recorded ? Sage : Paper;
            button.ForeColor = i == stage ? Paper : i <= recorded ? Ink : Muted;
            button.FlatAppearance.BorderColor = i == stage ? Ink : Color.FromArgb(205, 190, 158);
            button.FlatAppearance.BorderSize = i == stage ? 0 : 1;
            button.AccessibleName = plant.StageNames[i];
            button.AccessibleDescription = (i <= recorded ? "已记录" : "待解锁") + (i == stage ? "，当前图版" : "");
        }
    }

    private Button MakeTurnButton(Rectangle bounds, string text, int tabIndex)
    {
        var button = GardenWindow.MakeButton(this, bounds, text, paragraphFont);
        button.TabIndex = tabIndex;
        return button;
    }

    private static void SetTurnEnabled(Button button, bool enabled)
    {
        button.Enabled = enabled;
        button.BackColor = enabled ? Ink : Color.FromArgb(225, 228, 212);
        button.ForeColor = enabled ? Paper : Muted;
    }

    private static Label BookLabel(Control book, int x, int y, int width, int height, Font font, Color ink,
        ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        var label = GardenWindow.AddLabel(book, new Rectangle(x, y, width, height), "", font, ink, alignment);
        label.BackColor = Color.Transparent;
        return label;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        speciesFont.Dispose();
        latinFont.Dispose();
        paragraphFont.Dispose();
    }

    private sealed class BookSpread : Panel
    {
        private static readonly Color Cover = Color.FromArgb(50, 77, 58);
        private static readonly Color PageEdge = Color.FromArgb(214, 192, 153);
        private static readonly Color Rule = Color.FromArgb(203, 184, 144);

        internal BookSpread() { DoubleBuffered = true; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var graphics = e.Graphics;
            var saved = graphics.Save();
            graphics.ScaleTransform(ClientSize.Width / 726f, ClientSize.Height / 474f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            GrowingSpaceStyle.FillRounded(graphics, new RectangleF(4, 15, 718, 452), 13, Color.FromArgb(47, 58, 58, 37));
            GrowingSpaceStyle.FillRounded(graphics, new RectangleF(4, 8, 718, 452), 11, Cover);
            GrowingSpaceStyle.FillRounded(graphics, new RectangleF(12, 8, 702, 443), 7, PageEdge);
            GrowingSpaceStyle.FillRounded(graphics, new RectangleF(13, 7, 700, 441), 7, Color.FromArgb(235, 219, 179));
            DrawLeaf(graphics, false);
            DrawLeaf(graphics, true);

            using (var rule = new Pen(Rule))
            {
                graphics.DrawLine(rule, 126, 56, 238, 56);
                graphics.DrawLine(rule, 397, 128, 687, 128);
                graphics.DrawLine(rule, 397, 262, 687, 262);
            }
            GrowingSpaceStyle.FillRounded(graphics, new RectangleF(397, 394, 290, 40), 6, Color.FromArgb(234, 231, 202));
            graphics.Restore(saved);
        }

        private static void DrawLeaf(Graphics graphics, bool right)
        {
            var state = graphics.Save();
            if (right)
            {
                graphics.TranslateTransform(726, 0);
                graphics.ScaleTransform(-1, 1);
            }
            using (var outline = new GraphicsPath())
            using (var gradient = new LinearGradientBrush(new RectangleF(14, 5, 349, 440), Paper, PageEdge, 0f))
            using (var edge = new Pen(Color.FromArgb(194, 172, 130), .75f))
            {
                gradient.InterpolationColors = new ColorBlend {
                    Positions = new[] { 0f, .12f, .76f, .94f, 1f },
                    Colors = new[] { Color.FromArgb(248, 238, 211), Color.FromArgb(255, 250, 233),
                        Color.FromArgb(253, 247, 229), Color.FromArgb(247, 234, 204), Color.FromArgb(190, 161, 117) }
                };
                outline.AddBezier(15, 12, 119, 4, 304, -1, 363, 18);
                outline.AddLine(363, 18, 363, 445);
                outline.AddBezier(363, 445, 296, 434, 99, 443, 15, 439);
                outline.CloseFigure();
                graphics.FillPath(gradient, outline);
                graphics.DrawPath(edge, outline);
            }
            graphics.Restore(state);
        }
    }

    private sealed class PlantPlate : Control
    {
        private readonly PlantArtwork artwork;
        private PlantSpecies plant;
        private int stage;
        private bool unlocked;

        internal PlantPlate(PlantArtwork artwork)
        {
            this.artwork = artwork;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            TabStop = false;
        }

        internal void ShowStage(PlantSpecies species, int selected, bool known)
        {
            plant = species;
            stage = selected;
            unlocked = known;
            AccessibleName = species.Name + " " + species.StageNames[selected] + (known ? " 图版" : " 未解锁");
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (plant == null || Width < 2 || Height < 2) return;
            var graphics = e.Graphics;
            var saved = graphics.Save();
            graphics.ScaleTransform(Width / 292f, Height / 303f);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(5, 3, 282, 295);
            using (var outline = GrowingSpaceStyle.Rounded(bounds, 5))
            using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb(250, 247, 226), Color.FromArgb(235, 233, 203), 90f))
            using (var frame = new Pen(Color.FromArgb(218, 200, 157)))
            {
                graphics.FillPath(gradient, outline);
                graphics.DrawPath(frame, outline);
                graphics.DrawRectangle(frame, 11, 9, 270, 283);
            }
            using (var guide = new Pen(Color.FromArgb(125, 190, 182, 139)) { DashStyle = DashStyle.Dot })
                graphics.DrawEllipse(guide, 47, 48, 198, 198);

            if (unlocked)
            {
                artwork.DrawPreview(graphics, plant, stage, new RectangleF(24, 18, 244, 266));
            }
            else
            {
                using (var ink = new Pen(Gold, 2.5f))
                {
                    graphics.DrawArc(ink, 136, 129, 20, 27, 180, 180);
                    using (var box = GrowingSpaceStyle.Rounded(new RectangleF(129, 142, 34, 26), 5))
                        graphics.DrawPath(ink, box);
                    graphics.DrawLine(ink, 146, 152, 146, 159);
                }
            }
            graphics.Restore(saved);
        }
    }
}

