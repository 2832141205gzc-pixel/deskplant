using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class PlantProgressWindow : Form
{
    private static readonly Color Green = Color.FromArgb(34, 81, 63);
    private static readonly Color Muted = Color.FromArgb(80, 99, 88);
    private readonly PlantState state;
    private readonly Label stage, percent, moisture, speed, note;
    private readonly Label[] milestones;
    private readonly Panel bar;
    private readonly Timer timer = new Timer { Interval = 1000 };
    private readonly Font displayFont = new Font("Microsoft YaHei UI", 17, FontStyle.Bold);
    private readonly Font bodyFont = new Font("Microsoft YaHei UI", 9, FontStyle.Regular);
    private readonly Font emphaticFont = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
    private double progress;

    internal PlantProgressWindow(PlantState state)
    {
        this.state = state;
        var species = state.Active;
        milestones = new Label[species.StageNames.Length];
        Text = "生长进度";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = MinimizeBox = false;
        BackColor = Color.FromArgb(254, 249, 242);
        ClientSize = new Size(330, 211);

        stage = AddLabel(this, 17, 14, 194, 39, displayFont, ContentAlignment.MiddleLeft);
        percent = AddLabel(this, 209, 14, 104, 39, displayFont, ContentAlignment.MiddleRight);
        bar = new Panel { Bounds = new Rectangle(20, 63, 290, 9), BackColor = Color.FromArgb(220, 231, 219) };
        bar.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var fill = new SolidBrush(Color.FromArgb(77, 147, 113)))
                e.Graphics.FillRectangle(fill, 0, 0, (int)Math.Round(bar.ClientSize.Width * progress / 100), bar.ClientSize.Height);
        };
        Controls.Add(bar);

        for (int i = 0; i < milestones.Length; i++)
        {
            var alignment = i == 0 ? ContentAlignment.MiddleLeft : i == milestones.Length - 1 ? ContentAlignment.MiddleRight : ContentAlignment.MiddleCenter;
            var width = 296 / milestones.Length;
            milestones[i] = AddLabel(this, 17 + width * i, 81, width, 26, bodyFont, alignment);
            milestones[i].Text = species.StageNames[i] + " " + species.Milestones[i] + "%";
        }

        var care = new Panel { Bounds = new Rectangle(20, 123, 290, 41), BackColor = Color.FromArgb(232, 240, 227) };
        Controls.Add(care);
        moisture = AddLabel(care, 10, 0, 111, 41, emphaticFont, ContentAlignment.MiddleLeft);
        speed = AddLabel(care, 122, 0, 158, 41, emphaticFont, ContentAlignment.MiddleRight);
        note = AddLabel(this, 17, 173, 296, 25, bodyFont, ContentAlignment.MiddleLeft);
        note.ForeColor = Muted;

        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        timer.Tick += delegate { RefreshReading(); };
        RefreshReading();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Owner != null)
        {
            var plant = Owner.Bounds;
            var screen = Screen.FromRectangle(plant).WorkingArea;
            var x = plant.Left - Width - 12;
            if (x < screen.Left) x = plant.Right + 12;
            Location = new Point(Math.Max(screen.Left, Math.Min(x, screen.Right - Width)),
                                 Math.Max(screen.Top, Math.Min(plant.Bottom - Height, screen.Bottom - Height)));
        }
        timer.Start();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData != Keys.Escape) return base.ProcessCmdKey(ref message, keyData);
        Close();
        return true;
    }

    internal void RefreshReading()
    {
        var reading = state.Read(DateTime.UtcNow);
        if (reading.Species == null) return;
        stage.Text = reading.StageName;
        percent.Text = reading.ProgressText;
        moisture.Text = "水分 " + reading.MoisturePercent + "%";
        speed.Text = reading.Mature ? "成长完成" : "成长速度  " + reading.GrowthSpeed.ToString("0.#") + "×";
        note.Text = reading.Mature ? (reading.Species.IsCrop ? "已结果，可以移入果园。" : "已长成，可以移入花园。") :
            "水分达到 " + PlantState.RichMoisture + "% 时，成长速度加倍。";
        for (int i = 0; i < milestones.Length; i++)
        {
            milestones[i].ForeColor = i == reading.Stage ? Green : Muted;
            milestones[i].Font = i == reading.Stage ? emphaticFont : bodyFont;
        }
        if (progress != reading.Progress)
        {
            progress = reading.Progress;
            bar.Invalidate();
        }
    }

    private static Label AddLabel(Control parent, int x, int y, int width, int height, Font font, ContentAlignment alignment)
    {
        var label = new Label { Bounds = new Rectangle(x, y, width, height), ForeColor = Green, Font = font, TextAlign = alignment };
        parent.Controls.Add(label);
        return label;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        timer.Dispose();
        displayFont.Dispose();
        bodyFont.Dispose();
        emphaticFont.Dispose();
    }
}
