using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class PlantWindow : Form
{
    private const int CanvasWidth = 238, CanvasHeight = 278;
    private static readonly Rectangle ZoomHandle = new Rectangle(8, 235, 24, 31);
    private static readonly Rectangle WaterButton = new Rectangle(43, 235, 74, 31);
    private static readonly Rectangle ChooseButton = new Rectangle(43, 235, 152, 31);
    private static readonly Rectangle PlantArea = new Rectangle(29, 6, 180, 217);
    private static readonly Color Ink = Color.FromArgb(55, 72, 62);
    private static readonly Color Green = Color.FromArgb(34, 81, 63);
    private readonly PlantState state;
    private readonly PlantArtwork artwork = new PlantArtwork();
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private readonly NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly PlantReminders reminders = new PlantReminders();
    private readonly ToolStripMenuItem toggleItem, progressItem, zoomMenu, waterItem, archiveItem, uprootItem;
    private readonly Timer animationTimer = new Timer { Interval = PlantSway.FrameInterval };
    private readonly Timer refreshTimer = new Timer { Interval = 15000 };
    private readonly Timer orchardTimer = new Timer { Interval = 1000 };
    private readonly Stopwatch watering = new Stopwatch();
    private LayeredSurface surface;
    private WateringEffect wateringEffect;
    private Decoration wateringStyle;
    private bool reminderOpensOrchard;
    private PlantProgressWindow progressWindow;
    private GardenWindow gardenWindow;
    private bool waterAdded, waterMotion, showFeedback, showingArchive, listeningToDisplay, released;
    private MouseAction mouseAction;
    private PointF grabbedAt;
    private Point resizeAnchor;
    private int resizeStartX, resizeStartZoom, zoom;
    private float dpiScale = 1, resizeStartDpi;
    private float ScaleFactor { get { return dpiScale * zoom / 100f; } }

    internal PlantWindow(PlantState state)
    {
        this.state = state;
        zoom = state.Zoom;
        Text = AppEdition.WindowTitle;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(Scaled(CanvasWidth), Scaled(CanvasHeight));
        Location = state.Position.HasValue ? ClampToScreen(state.Position.Value) : DefaultLocation();

        trayIcon = CreateTrayIcon();
        waterItem = new ToolStripMenuItem("浇水", null, delegate { ShowPlant(); WaterPlant(); });
        menu.Items.Add(waterItem);
        progressItem = new ToolStripMenuItem("查看生长进度", null, delegate { OpenProgress(); });
        menu.Items.Add(progressItem);
        menu.Items.Add("打开花园", null, delegate { OpenGarden(); });
        menu.Items.Add("打开果园", null, delegate { OpenGarden(1); });
        menu.Items.Add("打开仓库", null, delegate { OpenGarden(2); });
        menu.Items.Add("打开回收站", null, delegate { OpenGarden(3); });
        menu.Items.Add("打开植物图鉴", null, delegate { OpenGarden(GardenWindow.JournalSection); });
        menu.Items.Add("打开金币商店", null, delegate { OpenGarden(GardenWindow.ShopSection); });
#if DESKPLANT_TEST
        menu.Items.Add("打开测试工具", null, delegate { OpenGarden(GardenWindow.TestSection); });
#endif
        var reminderMenu = new ToolStripMenuItem("提醒设置");
        var waterReminder = new ToolStripMenuItem("浇水提醒", null, delegate { ToggleReminder(true); });
        var harvestReminder = new ToolStripMenuItem("采摘提醒", null, delegate { ToggleReminder(false); });
        reminderMenu.DropDownItems.AddRange(new[] { waterReminder, harvestReminder });
        reminderMenu.DropDownOpening += delegate
        {
            waterReminder.Checked = state.WaterReminderEnabled;
            harvestReminder.Checked = state.HarvestReminderEnabled;
        };
        menu.Items.Add(reminderMenu);
        archiveItem = new ToolStripMenuItem("移入花园", null, delegate { ArchiveCurrent(); });
        menu.Items.Add(archiveItem);
        uprootItem = new ToolStripMenuItem("铲除盆栽", null, delegate { UprootDesktop(); });
        menu.Items.Add(uprootItem);
        menu.Items.Add(new ToolStripSeparator());
        zoomMenu = new ToolStripMenuItem("调整大小");
        foreach (int percent in new[] { PlantState.MinimumZoom, 80, 100, 125, 150, PlantState.MaximumZoom })
        {
            var item = new ToolStripMenuItem(percent + "%") { Tag = percent };
            item.Click += delegate { ShowPlant(); ChangeZoom(percent, new Point(Right, Bottom)); FinishMove(); };
            zoomMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(zoomMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("移回右下角", null, delegate { ShowPlant(); MoveTo(BottomRight()); });
        toggleItem = new ToolStripMenuItem("隐藏盆栽", null, delegate { ToggleVisible(); });
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { Close(); });
        menu.Opening += delegate
        {
            var reading = state.Read(DateTime.UtcNow);
            var hasPlant = reading.Species != null;
            waterItem.Enabled = progressItem.Enabled = uprootItem.Enabled = hasPlant;
            archiveItem.Enabled = reading.Mature;
            archiveItem.Text = reading.Species != null && reading.Species.IsCrop ? "移入果园" : "移入花园";
            progressItem.Text = hasPlant ? "查看生长进度…  " + reading.StageName + " " + reading.ProgressText : "查看生长进度";
            zoomMenu.Text = "调整大小  " + zoom + "%";
            foreach (ToolStripMenuItem item in zoomMenu.DropDownItems) item.Checked = (int)item.Tag == zoom;
            toggleItem.Text = Visible ? "隐藏盆栽" : "显示盆栽";
        };
        tray = new NotifyIcon { Icon = trayIcon, Text = Text, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ToggleVisible();
        };
        tray.BalloonTipClicked += delegate { if (reminderOpensOrchard) OpenGarden(1); else ShowPlant(); };

        animationTimer.Tick += delegate
        {
            var feedbackEnded = showFeedback && watering.ElapsedMilliseconds >= (waterMotion ? WateringEffect.Duration : 1250);
            if (feedbackEnded) showFeedback = false;
            if (Visible && (state.MotionEnabled || feedbackEnded)) PaintPlant();
            if (feedbackEnded) SyncAnimationTimer();
        };
        refreshTimer.Tick += delegate
        {
            var now = DateTime.UtcNow;
            state.RecordGrowth(now);
            CheckReminders(now);
            if (Visible) PaintPlant();
        };
        orchardTimer.Tick += delegate
        {
            if (state.AutoHarvestEnabled) Harvest(null, true);
            if (state.PurgeExpired(DateTime.UtcNow) && gardenWindow != null && !gardenWindow.IsDisposed)
                gardenWindow.RefreshRecycle();
        };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var settings = base.CreateParams;
            settings.ExStyle |= DesktopNative.LayeredWindow | DesktopNative.ToolWindow | DesktopNative.NoActivate;
            return settings;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var dpi = DesktopNative.GetDpiForWindow(Handle);
        dpiScale = dpi > 0 ? dpi / 96f : 1;
        Location = state.Position.HasValue ? ClampToScreen(state.Position.Value) : DefaultLocation();
        PaintPlant();
        SyncAnimationTimer();
        refreshTimer.Start();
        orchardTimer.Start();
        if (state.AutoHarvestEnabled) Harvest(null, true);
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        listeningToDisplay = true;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == DesktopNative.MouseActivate)
        {
            message.Result = new IntPtr(3);
            return;
        }
        if (message.Msg == DesktopNative.ShowPlantMessage && DesktopNative.ShowPlantMessage != 0)
        {
            if (tray != null) ShowPlant();
            return;
        }
        if (message.Msg == DesktopNative.DpiChanged && IsHandleCreated)
        {
            dpiScale = (message.WParam.ToInt64() & 0xffff) / 96f;
            var suggested = (RectangleFromWindows)Marshal.PtrToStructure(message.LParam, typeof(RectangleFromWindows));
            if (mouseAction == MouseAction.Move) Location = DragLocation();
            else if (mouseAction == MouseAction.Resize) Location = LocationFromBottomRight(resizeAnchor);
            else Location = new Point(suggested.Left, suggested.Top);
            if (Visible) PaintPlant();
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        grabbedAt = ToLogical(e.Location);
        var point = Point.Round(grabbedAt);
        var primary = state.Active == null ? ChooseButton : WaterButton;
        if (primary.Contains(point)) mouseAction = showingArchive ? MouseAction.Archive : MouseAction.Primary;
        else if (ZoomHandle.Contains(point))
        {
            mouseAction = MouseAction.Resize;
            resizeAnchor = new Point(Right, Bottom);
            resizeStartX = Cursor.Position.X;
            resizeStartZoom = zoom;
            resizeStartDpi = dpiScale;
            Cursor = Cursors.SizeWE;
        }
        else if (PlantArea.Contains(point))
        {
            mouseAction = MouseAction.Move;
            Cursor = Cursors.SizeAll;
        }
        else return;
        Capture = true;
        if (mouseAction == MouseAction.Resize) PaintPlant();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (mouseAction == MouseAction.Move)
        {
            Location = DragLocation();
            return;
        }
        if (mouseAction == MouseAction.Resize)
        {
            var gripCenterX = ZoomHandle.Left + ZoomHandle.Width / 2;
            var change = (resizeStartX - Cursor.Position.X) * 100 / ((CanvasWidth - gripCenterX) * resizeStartDpi);
            ChangeZoom(resizeStartZoom + (int)Math.Round(change), resizeAnchor);
            return;
        }

        var point = Point.Round(ToLogical(e.Location));
        if ((state.Active == null ? ChooseButton : WaterButton).Contains(point)) Cursor = Cursors.Hand;
        else if (ZoomHandle.Contains(point)) Cursor = Cursors.SizeWE;
        else Cursor = PlantArea.Contains(point) ? Cursors.SizeAll : Cursors.Arrow;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            menu.Show(Cursor.Position);
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        var action = mouseAction;
        mouseAction = MouseAction.None;
        Capture = false;
        if (action == MouseAction.Move || action == MouseAction.Resize) FinishMove();
        if (action == MouseAction.Resize) PaintPlant();
        var point = Point.Round(ToLogical(e.Location));
        if (action == MouseAction.Archive && WaterButton.Contains(point)) ArchiveCurrent();
        if (action == MouseAction.Primary && (state.Active == null ? ChooseButton : WaterButton).Contains(point))
        {
            if (state.Active == null) OpenGarden(0, true);
            else WaterPlant();
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture) return;
        var action = mouseAction;
        mouseAction = MouseAction.None;
        if (action == MouseAction.Move || action == MouseAction.Resize) FinishMove();
        if (action == MouseAction.Resize && Visible) PaintPlant();
    }

    private void WaterPlant()
    {
        if (state.Active == null) return;
        waterAdded = state.Water(DateTime.UtcNow);
        waterMotion = waterAdded && state.MotionEnabled;
        if (waterMotion)
        {
            var style = state.Equipped(DecorationKind.WateringCan);
            if (wateringEffect == null || wateringStyle != style)
            {
                if (wateringEffect != null) wateringEffect.Dispose();
                wateringEffect = new WateringEffect(style);
                wateringStyle = style;
            }
        }
        showFeedback = true;
        watering.Restart();
        PaintPlant();
        if (progressWindow != null && !progressWindow.IsDisposed) progressWindow.RefreshReading();
        if (gardenWindow != null && !gardenWindow.IsDisposed) gardenWindow.RefreshGarden();
        SyncAnimationTimer();
        CheckReminders(DateTime.UtcNow);
    }

    private void ToggleReminder(bool watering)
    {
        bool enabled = watering ? state.WaterReminderEnabled : state.HarvestReminderEnabled;
        if (!state.SetReminder(watering, !enabled))
            MessageBox.Show("提醒设置暂时无法保存，请重试。", "DeskPlant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else CheckReminders(DateTime.UtcNow);
    }

    private void CheckReminders(DateTime now)
    {
        bool orchard;
        var message = reminders.Check(state, now, out orchard);
        if (message.Length == 0) return;
        reminderOpensOrchard = orchard;
        tray.ShowBalloonTip(7000, "DeskPlant 提醒", message, ToolTipIcon.Info);
    }

    private void OpenProgress()
    {
        if (state.Active == null) return;
        ShowPlant();
        if (progressWindow == null || progressWindow.IsDisposed)
        {
            progressWindow = new PlantProgressWindow(state) { Location = Location };
            progressWindow.Show(this);
        }
        progressWindow.Activate();
    }

    private void OpenGarden(int section = 0, bool chooseSeed = false)
    {
        ShowPlant();
        if (gardenWindow == null || gardenWindow.IsDisposed)
        {
            gardenWindow = new GardenWindow(state, artwork, ArchiveCurrent, SowNext, ChangeMotion,
                plot => Harvest(plot, false), SetAutoHarvest, UprootGarden, UprootPlot, RestorePlant) { Location = Location };
            gardenWindow.DecorationChanged += delegate { if (Visible) PaintPlant(); };
#if DESKPLANT_TEST
            gardenWindow.TestChanged += RefreshAfterTransfer;
#endif
            gardenWindow.SelectSection(section, chooseSeed);
            gardenWindow.Show(this);
        }
        else gardenWindow.SelectSection(section, chooseSeed);
        gardenWindow.RefreshGarden();
        gardenWindow.Activate();
    }

    private void ChangeMotion(bool enabled)
    {
        if (!state.SetMotion(enabled))
        {
            MessageBox.Show(gardenWindow, "动效设置暂时保存失败，请重试。", "我的花园", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!enabled) waterMotion = false;
        if (Visible) PaintPlant();
        SyncAnimationTimer();
        gardenWindow.RefreshGarden();
    }

    private void SyncAnimationTimer()
    {
        var moving = state.MotionEnabled && state.Active != null;
        var interval = moving ? PlantSway.FrameInterval : 125;
        if (animationTimer.Interval != interval) animationTimer.Interval = interval;
        animationTimer.Enabled = Visible && (moving || showFeedback);
    }

    private void ArchiveCurrent()
    {
        var now = DateTime.UtcNow;
        var reading = state.Read(now);
        if (!reading.Mature) return;
        if (!state.Archive(now))
        {
            MessageBox.Show("种植空间暂时保存失败，请重试。", "我的种植空间", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (progressWindow != null && !progressWindow.IsDisposed) progressWindow.Close();
        showFeedback = waterMotion = false;
        OpenGarden(reading.Species.IsCrop ? 1 : 0);
    }

    private void Harvest(OrchardPlot plot, bool automatic)
    {
        var picks = state.Harvest(DateTime.UtcNow, plot, automatic);
        if (picks == null)
        {
            if (!automatic) MessageBox.Show(gardenWindow, "收成暂时无法入库，请重试。", "果园", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (picks.Count > 0 && gardenWindow != null && !gardenWindow.IsDisposed) gardenWindow.ShowHarvest(picks);
        if (picks.Count > 0 && !automatic) CheckReminders(DateTime.UtcNow);
    }

    private void SetAutoHarvest(bool enabled)
    {
        if (!state.SetAutoHarvest(enabled, DateTime.UtcNow))
            MessageBox.Show(gardenWindow, "自动采摘设置暂时无法保存，请重试。", "果园", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (enabled) Harvest(null, true);
        gardenWindow.RefreshGarden();
    }

    private void UprootDesktop()
    {
        if (state.Active == null) return;
        if (!state.UprootDesktop(DateTime.UtcNow)) { TransferError(); return; }
        RefreshAfterTransfer();
        OpenGarden(3);
    }

    private void UprootGarden(PlantSpecies species)
    {
        if (!state.UprootGarden(species, DateTime.UtcNow)) { TransferError(); return; }
        gardenWindow.RefreshGarden();
    }

    private void UprootPlot(OrchardPlot plot)
    {
        if (!state.UprootPlot(plot, DateTime.UtcNow)) { TransferError(); return; }
        gardenWindow.RefreshGarden();
    }

    private void RestorePlant(RecycledPlant entry)
    {
        if (entry.IsExpired(DateTime.UtcNow)) { gardenWindow.RefreshRecycle(); return; }
        var exchange = entry.Location == PlantLocation.Desktop && state.Active != null;
        if (exchange && MessageBox.Show(gardenWindow, "将当前桌面盆栽移入回收站，再恢复选中的盆栽？", "恢复到桌面",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        if (!state.Restore(entry, DateTime.UtcNow, exchange))
        {
            if (!entry.IsExpired(DateTime.UtcNow)) TransferError();
            gardenWindow.RefreshRecycle();
            return;
        }
        RefreshAfterTransfer();
    }

    private void RefreshAfterTransfer()
    {
        if (progressWindow != null && !progressWindow.IsDisposed) progressWindow.Close();
        showFeedback = waterMotion = false;
        if (Visible) PaintPlant();
        SyncAnimationTimer();
        if (gardenWindow != null && !gardenWindow.IsDisposed) gardenWindow.RefreshGarden();
    }

    private void TransferError()
    {
        MessageBox.Show("植物变更暂时无法保存，请重试。", "回收站", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void SowNext(PlantSpecies species)
    {
        if (state.Active != null) return;
        if (!state.Sow(species, DateTime.UtcNow))
        {
            MessageBox.Show("新盆栽暂时保存失败，请重试。", "我的花园", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (gardenWindow != null && !gardenWindow.IsDisposed) gardenWindow.Close();
        ShowPlant();
    }

    private void ToggleVisible()
    {
        if (Visible)
        {
            if (progressWindow != null && !progressWindow.IsDisposed) progressWindow.Close();
            if (gardenWindow != null && !gardenWindow.IsDisposed) gardenWindow.Close();
            Hide();
            SyncAnimationTimer();
        }
        else ShowPlant();
    }

    private void ShowPlant()
    {
        if (!Visible) Show();
        Location = ClampToScreen(Location);
        PaintPlant();
        SyncAnimationTimer();
    }

    private void MoveTo(Point position)
    {
        Location = position;
        state.RememberView(Location, zoom);
    }

    private void FinishMove() { MoveTo(ClampToScreen(Location)); }

    private Point LocationFromBottomRight(Point anchor)
    {
        return new Point(anchor.X - Scaled(CanvasWidth), anchor.Y - Scaled(CanvasHeight));
    }

    private void ChangeZoom(int percent, Point anchor)
    {
        percent = PlantState.ClampZoom(percent);
        if (zoom == percent) return;
        zoom = percent;
        Location = LocationFromBottomRight(anchor);
        PaintPlant();
    }

    private void DisplaySettingsChanged(object sender, EventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            BeginInvoke((Action)delegate
            {
                if (IsDisposed || mouseAction != MouseAction.None) return;
                var position = ClampToScreen(Location);
                if (position != Location) MoveTo(position);
                if (Visible) PaintPlant();
            });
        }
        catch (InvalidOperationException) { }
    }

    private PointF ToLogical(Point p) { return new PointF(p.X / ScaleFactor, p.Y / ScaleFactor); }
    private int Scaled(int pixels) { return (int)Math.Round(pixels * ScaleFactor); }

    private Point DragLocation()
    {
        var cursor = Cursor.Position;
        return new Point(cursor.X - (int)Math.Round(grabbedAt.X * ScaleFactor),
                         cursor.Y - (int)Math.Round(grabbedAt.Y * ScaleFactor));
    }

    private Point BottomRight()
    {
        var area = Screen.PrimaryScreen.WorkingArea;
        return new Point(Math.Max(area.Left, area.Right - Scaled(CanvasWidth) - (int)Math.Round(24 * dpiScale)),
                         Math.Max(area.Top, area.Bottom - Scaled(CanvasHeight) - (int)Math.Round(16 * dpiScale)));
    }

    private Point DefaultLocation()
    {
        var corner = BottomRight();
#if DESKPLANT_TEST
        corner.X = Screen.PrimaryScreen.WorkingArea.Left + (int)Math.Round(24 * dpiScale);
#endif
        return corner;
    }

    private Point ClampToScreen(Point position)
    {
        var width = Scaled(CanvasWidth);
        var height = Scaled(CanvasHeight);
        var area = Screen.FromRectangle(new Rectangle(position, new Size(width, height))).WorkingArea;
        return new Point(Math.Max(area.Left, Math.Min(position.X, area.Right - width)),
                         Math.Max(area.Top, Math.Min(position.Y, area.Bottom - height)));
    }

    private void PaintPlant()
    {
        var size = new Size(Scaled(CanvasWidth), Scaled(CanvasHeight));
        if (surface == null || surface.Size != size)
        {
            if (surface != null) surface.Dispose();
            surface = new LayeredSurface(size.Width, size.Height);
        }

        var reading = state.Read(DateTime.UtcNow);
        showingArchive = reading.Mature;
        using (var graphics = surface.Draw())
        {
            graphics.Clear(Color.Transparent);
            graphics.ScaleTransform(ScaleFactor, ScaleFactor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (reading.Species != null)
            {
                DrawPlant(graphics, reading);
                DrawControls(graphics, reading);
                if (showFeedback && waterMotion)
                    wateringEffect.Draw(graphics, artwork.SoilPosition(reading.Species, PlantArea), watering.ElapsedMilliseconds);
            }
            else
            {
                DrawDock(graphics);
                RoundedFill(graphics, ChooseButton, 15, Green);
                CenteredText(graphics, "选择下一株", ChooseButton, 12, Color.White, FontStyle.Bold);
            }
            DrawZoomHandle(graphics);
#if DESKPLANT_TEST
            var badge = new RectangleF(205, 238, 30, 22);
            RoundedFill(graphics, badge, 9, Color.FromArgb(240, 229, 213));
            CenteredText(graphics, "测试", badge, 10, Ink, FontStyle.Bold);
#endif
        }
        surface.Present(Handle, Location);
    }

    private void DrawPlant(Graphics graphics, PlantReading reading)
    {
        using (var shadow = new SolidBrush(Color.FromArgb(34, 21, 29, 22)))
            graphics.FillEllipse(shadow, 66, 209, 105, 11);
        using (var shadow = new SolidBrush(Color.FromArgb(25, 21, 29, 22)))
            graphics.FillEllipse(shadow, 77, 212, 85, 6);

        artwork.Draw(graphics, reading.Species, reading.Stage, PlantArea, state.MotionEnabled, state.Equipped(DecorationKind.Pot));
    }

    private void DrawControls(Graphics graphics, PlantReading reading)
    {
        DrawDock(graphics);
        RoundedFill(graphics, WaterButton, 15, showFeedback && waterAdded ? Color.FromArgb(45, 110, 82) : Green);

        var buttonText = reading.Mature ? (reading.Species.IsCrop ? "移入果园" : "移入花园") : showFeedback ? (waterAdded ? "已浇水" : "水分充足") : "浇水";
        var showDrop = !reading.Mature && !showFeedback;
        if (showDrop) DrawDrop(graphics, 65, 250, 5.5f, Color.FromArgb(219, 243, 230));
        CenteredText(graphics, buttonText, showDrop ? new RectangleF(72, 235, 31, 31) : WaterButton,
            showDrop ? 12 : 11, Color.White, FontStyle.Bold);

        var moisture = reading.MoisturePercent;
        CenteredText(graphics, "水分 " + moisture + "%", new RectangleF(122, 233, 71, 19), 10.5f, Ink, FontStyle.Regular);
        RoundedFill(graphics, new RectangleF(127, 255, 61, 5), 2.5f, Color.FromArgb(222, 230, 220));
        if (moisture > 0)
            RoundedFill(graphics, new RectangleF(127, 255, 61 * moisture / 100f, 5), 2.5f, Color.FromArgb(74, 146, 113));

    }

    private static void DrawDock(Graphics graphics)
    {
        RoundedFill(graphics, new RectangleF(36, 230, 166, 43), 20, Color.FromArgb(49, 24, 32, 25));
        RoundedFill(graphics, new RectangleF(36, 228, 166, 43), 20, Color.FromArgb(254, 249, 242));
    }

    private void DrawZoomHandle(Graphics graphics)
    {
        RoundedFill(graphics, ZoomHandle, 12, mouseAction == MouseAction.Resize ? Color.FromArgb(222, 235, 217) : Color.FromArgb(254, 249, 242));
        using (var pen = new Pen(Green, 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            graphics.DrawLines(pen, new[] { new PointF(18, 247), new PointF(14, 250.5f), new PointF(18, 254) });
            graphics.DrawLines(pen, new[] { new PointF(22, 247), new PointF(26, 250.5f), new PointF(22, 254) });
            graphics.DrawLine(pen, 14.5f, 250.5f, 25.5f, 250.5f);
        }
    }

    private static void DrawDrop(Graphics graphics, float x, float y, float radius, Color color)
    {
        using (var shape = new GraphicsPath())
        using (var brush = new SolidBrush(color))
        {
            shape.AddBezier(x, y - radius, x - radius * .3f, y - radius * .55f,
                            x - radius * .7f, y - radius * .12f, x - radius * .7f, y + radius * .22f);
            shape.AddBezier(x - radius * .7f, y + radius * .22f, x - radius * .7f, y + radius,
                            x + radius * .7f, y + radius, x + radius * .7f, y + radius * .22f);
            shape.AddBezier(x + radius * .7f, y + radius * .22f, x + radius * .7f, y - radius * .12f,
                            x + radius * .3f, y - radius * .55f, x, y - radius);
            shape.CloseFigure();
            graphics.FillPath(brush, shape);
        }
    }

    private static void CenteredText(Graphics graphics, string text, RectangleF bounds, float size, Color color, FontStyle style)
    {
        using (var font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            graphics.DrawString(text, font, brush, bounds, format);
    }

    private static void RoundedFill(Graphics graphics, RectangleF rect, float radius, Color color)
    {
        var diameter = Math.Min(2 * radius, Math.Min(rect.Width, rect.Height));
        using (var path = new GraphicsPath())
        using (var brush = new SolidBrush(color))
        {
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }

    private Icon CreateTrayIcon()
    {
        using (var image = artwork.Preview(PlantCatalog.Jade, PlantCatalog.Jade.FinalStage, new Size(32, 32)))
        {
            var handle = image.GetHicon();
            try
            {
                using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone();
            }
            finally { DesktopNative.DestroyIcon(handle); }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !released)
        {
            released = true;
            if (listeningToDisplay) SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            animationTimer.Dispose();
            refreshTimer.Dispose();
            orchardTimer.Dispose();
            if (tray != null) tray.Dispose();
            menu.Dispose();
            if (trayIcon != null) trayIcon.Dispose();
            artwork.Dispose();
            if (wateringEffect != null) wateringEffect.Dispose();
            if (surface != null) surface.Dispose();
        }
        base.Dispose(disposing);
    }

    private enum MouseAction { None, Move, Resize, Primary, Archive }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectangleFromWindows
    {
        internal int Left, Top, Right, Bottom;
    }
}
