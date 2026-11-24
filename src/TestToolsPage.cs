#if DESKPLANT_TEST
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class TestToolsPage : Panel
{
    private static readonly Color Green = Color.FromArgb(34, 81, 63);
    private static readonly Color Muted = Color.FromArgb(78, 96, 85);
    private readonly PlantState state;
    private readonly Action changed;
    private readonly ComboBox species, stage;
    private readonly NumericUpDown quantity;
    private readonly CheckBox ready;
    private readonly Label feedback, inventory;
    private readonly Button addGarden, removeGarden, addPlots, removePlots, addFruit, removeFruit, uproot, ripen, ripenOne;

    internal TestToolsPage(PlantState state, Font body, Font name, Action changed)
    {
        this.state = state;
        this.changed = changed;
        Dock = DockStyle.Fill;
        GardenWindow.AddLabel(this, new Rectangle(20, 13, 676, 28), "布置测试场景", name, Green);
        GardenWindow.AddLabel(this, new Rectangle(20, 43, 676, 26), "这里的操作只写入测试版存档。铲除的植物仍可在回收站恢复。", body, Muted);

        GardenWindow.AddLabel(this, new Rectangle(20, 88, 60, 29), "品种", body, Green);
        species = new ComboBox { Bounds = new Rectangle(82, 87, 155, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = body };
        species.Items.AddRange(PlantCatalog.All.Select(plant => (object)plant.Name).ToArray());
        Controls.Add(species);
        GardenWindow.AddLabel(this, new Rectangle(257, 88, 58, 29), "阶段", body, Green);
        stage = new ComboBox { Bounds = new Rectangle(319, 87, 119, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = body };
        Controls.Add(stage);
        GardenWindow.AddLabel(this, new Rectangle(459, 88, 57, 29), "数量", body, Green);
        quantity = new NumericUpDown { Bounds = new Rectangle(516, 87, 81, 28), Minimum = 1, Maximum = 100, Value = 3, Font = body };
        Controls.Add(quantity);
        inventory = GardenWindow.AddLabel(this, new Rectangle(20, 122, 671, 30), "", body, Muted);

        GardenWindow.AddLabel(this, new Rectangle(20, 170, 92, 37), "桌面", name, Green);
        AddAction(120, 173, "放到桌面", body, delegate { return state.TestPlaceDesktop(Picked, stage.SelectedIndex, DateTime.UtcNow); }, "已按选定阶段放到桌面。");
        uproot = AddAction(262, 173, "铲除桌面盆栽", body, delegate { return state.UprootDesktop(DateTime.UtcNow); }, "桌面盆栽已移入回收站。");

        GardenWindow.AddLabel(this, new Rectangle(20, 232, 92, 37), "花园", name, Green);
        addGarden = AddAction(120, 235, "添加观赏植物", body, delegate { return state.TestAddGarden(Picked, Quantity); }, "已添加观赏植物。");
        removeGarden = AddAction(262, 235, "铲除观赏植物", body, delegate { return state.TestUprootGarden(Picked, Quantity, DateTime.UtcNow); }, "选中的观赏植物已移入回收站。");

        GardenWindow.AddLabel(this, new Rectangle(20, 294, 92, 37), "果园", name, Green);
        ready = new CheckBox { Bounds = new Rectangle(411, 295, 232, 33), Text = "新地块立即长满", Checked = true, Font = body, ForeColor = Green };
        Controls.Add(ready);
        addPlots = AddAction(120, 297, "添加独立地块", body, delegate { return state.TestAddPlots(Picked, Quantity, ready.Checked, DateTime.UtcNow); }, "已添加果园地块。");
        removePlots = AddAction(262, 297, "铲除作物", body, delegate { return state.TestUprootPlots(Picked, Quantity, DateTime.UtcNow); }, "选中的作物已移入回收站。");
        ripen = AddAction(120, 343, "让全园立即长满", body, delegate { return state.TestRipen(DateTime.UtcNow, true); }, "果园作物已全部长满。");
        ripenOne = AddAction(262, 343, "每块地成熟一颗", body, delegate { return state.TestRipen(DateTime.UtcNow, false); }, "尚未长满的地块各成熟了一颗。");

        GardenWindow.AddLabel(this, new Rectangle(20, 400, 92, 37), "仓库", name, Green);
        addFruit = AddAction(120, 403, "增加果实", body, delegate { return state.TestChangeFruit(Picked, Quantity); }, "已增加选定品种的果实。");
        removeFruit = AddAction(262, 403, "减少果实", body, delegate { return state.TestChangeFruit(Picked, -Quantity); }, "已减少选定品种的果实。");
        feedback = GardenWindow.AddLabel(this, new Rectangle(20, 463, 676, 36), "", body, Muted);

        species.SelectedIndexChanged += delegate
        {
            stage.Items.Clear();
            stage.Items.AddRange(Picked.StageNames.Cast<object>().ToArray());
            stage.SelectedIndex = 0;
            RefreshTools();
        };
        species.SelectedIndex = Array.IndexOf(PlantCatalog.All, PlantCatalog.Find("strawberry"));
    }

    private PlantSpecies Picked { get { return PlantCatalog.All[species.SelectedIndex]; } }
    private int Quantity { get { return (int)quantity.Value; } }

    internal void RefreshTools()
    {
        var picked = Picked;
        var gardenCount = state.CountOf(picked);
        var plotCount = state.Plots.Count(plot => plot.Crop == picked);
        var fruitCount = state.FruitOf(picked);
        inventory.Text = "桌面  " + (state.Active == null ? "空位" : state.Active.Name) + "     花园 " + gardenCount +
                         " 株     果园 " + plotCount + " 块     仓库 " + fruitCount + " 个果实";
        GardenWindow.SetActionEnabled(uproot, state.Active != null);
        GardenWindow.SetActionEnabled(addGarden, !picked.IsCrop);
        GardenWindow.SetActionEnabled(removeGarden, gardenCount > 0);
        GardenWindow.SetActionEnabled(addPlots, picked.IsCrop);
        GardenWindow.SetActionEnabled(removePlots, plotCount > 0);
        GardenWindow.SetActionEnabled(addFruit, picked.IsCrop);
        GardenWindow.SetActionEnabled(removeFruit, fruitCount > 0);
        GardenWindow.SetActionEnabled(ripen, state.Plots.Count > 0);
        GardenWindow.SetActionEnabled(ripenOne, state.Plots.Count > 0);
        ready.Enabled = picked.IsCrop;
    }

    private Button AddAction(int x, int y, string text, Font font, Func<bool> apply, string success)
    {
        var button = GardenWindow.MakeButton(this, new Rectangle(x, y, 132, 33), text, font);
        button.Click += delegate
        {
            if (!apply()) { feedback.Text = "操作未保存，请重试。"; return; }
            changed();
            feedback.Text = success;
        };
        return button;
    }
}
#endif
