using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class RecyclePage : Panel
{
    private static readonly Color Green = Color.FromArgb(34, 81, 63);
    private static readonly Color Muted = Color.FromArgb(79, 98, 85);
    private readonly PlantState state;
    private readonly PlantArtwork artwork;
    private readonly ListView list;
    private readonly PictureBox image;
    private readonly Label count, details;
    private readonly Button restore;
    private RecycledPlant[] shown = new RecycledPlant[0];

    internal RecyclePage(PlantState state, PlantArtwork artwork, Font body, Font name, Action<RecycledPlant> recover)
    {
        this.state = state;
        this.artwork = artwork;
        Size = new Size(710, 580);
        Dock = DockStyle.Fill;
        count = GardenWindow.AddLabel(this, new Rectangle(18, 13, 680, 31), "", name, Green);
        GardenWindow.AddLabel(this, new Rectangle(18, 47, 680, 26), "植物会在这里保留 7 天，到期自动清理。恢复后继续原来的成长或结果倒计时。", body, Muted);

        list = new ListView { Bounds = new Rectangle(18, 85, 474, 440), View = View.Details, FullRowSelect = true,
            MultiSelect = false, HideSelection = false, Font = body, ShowItemToolTips = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        list.Columns.Add("植物");
        list.Columns.Add("原位置");
        list.Columns.Add("到期时间");
        list.ClientSizeChanged += delegate { LayoutColumns(); };
        list.SelectedIndexChanged += delegate { ShowSelected(); };
        Controls.Add(list);

        image = new PictureBox { Bounds = new Rectangle(517, 85, 157, 175), SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(238, 242, 230), TabStop = false };
        Controls.Add(image);
        details = GardenWindow.AddLabel(this, new Rectangle(513, 267, 165, 51), "选择一株植物", body, Muted, ContentAlignment.MiddleCenter);
        restore = GardenWindow.MakeButton(this, new Rectangle(525, 328, 141, 35), "恢复", body);
        restore.Click += delegate
        {
            var selected = Selected;
            if (selected != null) recover(selected);
        };
        LayoutColumns();
        VisibleChanged += delegate { if (Visible) RefreshBin(); };
        RefreshBin();
    }

    internal void RefreshBin()
    {
        var now = DateTime.UtcNow;
        var current = state.RecycleBin.Where(entry => !entry.IsExpired(now)).OrderByDescending(entry => entry.RemovedAt).ToArray();
        count.Text = current.Length == 0 ? "回收站是空的" : current.Length + " 株可恢复";
        if (shown.SequenceEqual(current))
        {
            if (Visible && Selected == null && list.Items.Count > 0) list.Items[0].Selected = true;
            GardenWindow.SetActionEnabled(restore, Selected != null);
            return;
        }
        var selected = Selected;
        shown = current;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var entry in current)
        {
            var date = entry.ExpiresAt.ToLocalTime();
            var name = entry.Species.Name;
            var row = new ListViewItem(new[] { name, LocationName(entry), date.ToString("MM-dd HH:mm") }) {
                Tag = entry, ToolTipText = "到期于 " + date.ToString("yyyy-MM-dd HH:mm:ss")
            };
            list.Items.Add(row);
            if (entry == selected) row.Selected = true;
        }
        if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = true;
        list.EndUpdate();
        ShowSelected();
    }

    private RecycledPlant Selected { get { return list.SelectedItems.Count == 0 ? null : (RecycledPlant)list.SelectedItems[0].Tag; } }

    private void ShowSelected()
    {
        if (image == null || restore == null) return;
        var selected = Selected;
        var old = image.Image;
        image.Image = selected == null ? null : artwork.Preview(selected.Species,
            selected.Location == PlantLocation.Desktop ? selected.Species.StageAt(selected.Progress) : selected.Species.FinalStage,
            new Size(143, 158));
        if (old != null) old.Dispose();
        details.Text = selected == null ? "选择一株植物" : selected.Species.Name + "\n恢复到" + LocationName(selected);
        GardenWindow.SetActionEnabled(restore, selected != null && !selected.IsExpired(DateTime.UtcNow));
    }

    private void LayoutColumns()
    {
        if (list.Columns.Count != 3) return;
        var width = Math.Max(100, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
        list.Columns[0].Width = (int)(width * .36);
        list.Columns[1].Width = (int)(width * .23);
        list.Columns[2].Width = width - list.Columns[0].Width - list.Columns[1].Width;
    }

    private static string LocationName(RecycledPlant entry)
    {
        return entry.Location == PlantLocation.Desktop ? "桌面" : entry.Location == PlantLocation.Garden ? "花园" : "果园";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && image.Image != null)
        {
            var current = image.Image;
            image.Image = null;
            current.Dispose();
        }
        base.Dispose(disposing);
    }
}
