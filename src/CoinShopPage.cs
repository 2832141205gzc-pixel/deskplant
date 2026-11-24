using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class CoinShopPage : Panel
{
    private static readonly GrowingSpaceStyle Theme = GrowingSpaceStyle.Warehouse;
    private readonly PlantState state;
    private readonly ComboBox crop;
    private readonly NumericUpDown quantity;
    private readonly Label balance, quote, feedback;
    private readonly Button sell, maximum;
    private readonly Button[] actions = new Button[Decoration.All.Length];
    private readonly PictureBox[] pictures = new PictureBox[Decoration.All.Length];
    private readonly PlantSpecies[] crops = PlantCatalog.All.Where(plant => plant.IsCrop).ToArray();
    internal event Action Changed;

    internal CoinShopPage(PlantState state, PlantArtwork artwork, Font title, Font name, Font body)
    {
        this.state = state;
        Size = new Size(766, 610);
        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        var banner = new SpaceBanner("金币商店", "Warehouse", Theme, title, body);
        Controls.Add(banner);
        balance = banner.Summary;
        var trade = new Panel { Bounds = new Rectangle(20, 161, 726, 128), BackColor = Theme.Surface };
        Controls.Add(trade);
        GardenWindow.AddLabel(trade, new Rectangle(14, 7, 149, 26), "出售果实", name, Theme.Ink);
        crop = new ComboBox { Bounds = new Rectangle(14, 39, 175, 26), DropDownStyle = ComboBoxStyle.DropDownList, Font = body, AccessibleName = "出售品种" };
        crop.Items.AddRange(crops.Select(plant => (object)plant.Name).ToArray());
        trade.Controls.Add(crop);
        quantity = new NumericUpDown { Bounds = new Rectangle(201, 39, 135, 26), Font = body, ThousandsSeparator = true, AccessibleName = "出售数量" };
        trade.Controls.Add(quantity);
        maximum = GardenWindow.MakeButton(trade, new Rectangle(347, 37, 72, 29), "全部", body, Theme);
        maximum.Click += delegate { quantity.Value = quantity.Maximum; };
        sell = GardenWindow.MakeButton(trade, new Rectangle(573, 37, 136, 29), "出售换金币", body, Theme);
        sell.Click += delegate { Sell(); };
        quote = GardenWindow.AddLabel(trade, new Rectangle(14, 70, 688, 23), "", body, Theme.Muted);
        feedback = GardenWindow.AddLabel(trade, new Rectangle(14, 94, 688, 26), "", body, Theme.Ink);

        var goods = new Panel { Bounds = new Rectangle(20, 306, 726, 285), AutoScroll = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        Controls.Add(goods);
        for (int i = 0; i < Decoration.All.Length; i++)
        {
            var item = Decoration.All[i];
            var card = new Panel { Bounds = new Rectangle(236 * (i % 3), 142 * (i / 3), 224, 132), BackColor = Theme.Surface };
            goods.Controls.Add(card);
            var image = item.Resource == null ? artwork.Preview(PlantCatalog.Jade, PlantCatalog.Jade.FinalStage, new Size(180, 140)) : LoadPicture(item);
            pictures[i] = new PictureBox { Bounds = new Rectangle(16, 6, 192, 66), Image = image, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = item.Name };
            card.Controls.Add(pictures[i]);
            GardenWindow.AddLabel(card, new Rectangle(11, 72, 202, 23), item.Name, name, Theme.Ink, ContentAlignment.MiddleCenter);
            var action = GardenWindow.MakeButton(card, new Rectangle(20, 98, 184, 27), "", body, Theme);
            action.AccessibleName = item.Name;
            action.Click += delegate { BuyOrEquip(item); };
            actions[i] = action;
        }
        crop.SelectedIndexChanged += delegate { RefreshTrade(); };
        quantity.ValueChanged += delegate { UpdateQuote(); };
        crop.SelectedIndex = Math.Max(0, Array.FindIndex(crops, plant => state.FruitOf(plant) > 0));
        RefreshShop();
    }

    private PlantSpecies SelectedCrop { get { return crop.SelectedIndex < 0 ? null : crops[crop.SelectedIndex]; } }

    internal void RefreshShop()
    {
        balance.Text = "金币余额  " + state.Coins.ToString("N0");
        RefreshTrade();
        for (int i = 0; i < Decoration.All.Length; i++)
        {
            var item = Decoration.All[i];
            var equipped = state.Equipped(item.Kind) == item;
            var owned = state.Owns(item);
            actions[i].Text = equipped ? "已装备" : owned ? "装备" : "购买 · " + item.Price + " 金币";
            GardenWindow.SetActionEnabled(actions[i], !equipped && (owned || state.Coins >= item.Price), Theme);
        }
    }

    private void RefreshTrade()
    {
        quantity.Maximum = state.SaleLimit(SelectedCrop);
        if (quantity.Value == 0 && quantity.Maximum > 0) quantity.Value = 1;
        UpdateQuote();
    }

    private void UpdateQuote()
    {
        var plant = SelectedCrop;
        if (plant == null) return;
        var price = PlantState.FruitValue(plant);
        quote.Text = "库存 " + state.FruitOf(plant).ToString("N0") + " 个  ·  每个 " + price + " 金币  ·  本次获得 " + (quantity.Value * price).ToString("N0") + " 金币";
        GardenWindow.SetActionEnabled(sell, quantity.Value > 0 && quantity.Value <= state.SaleLimit(plant), Theme);
        GardenWindow.SetActionEnabled(maximum, quantity.Maximum > 0, Theme);
    }

    private void Sell()
    {
        var plant = SelectedCrop;
        var count = (long)quantity.Value;
        if (!state.SellFruit(plant, count)) { ShowResult("出售未完成，请检查库存后重试。"); return; }
        ShowResult("已出售 " + count.ToString("N0") + " 个" + plant.Name + "，获得 " + (count * PlantState.FruitValue(plant)).ToString("N0") + " 金币。");
    }

    private void BuyOrEquip(Decoration item)
    {
        if (state.Owns(item))
            ShowResult(state.Equip(item) ? "已装备「" + item.Name + "」。" : "装备未保存，请重试。");
        else ShowResult(state.Purchase(item) ? "已购买「" + item.Name + "」，可点击装备。" : "购买未完成，请检查金币余额后重试。");
    }

    private void ShowResult(string message)
    {
        feedback.Text = message;
        RefreshShop();
        if (Changed != null) Changed();
    }

    private static Bitmap LoadPicture(Decoration item)
    {
        using (var stream = typeof(CoinShopPage).Assembly.GetManifestResourceStream(item.Resource))
        using (var image = new Bitmap(stream))
        {
            var crop = PlantArtwork.CleanAndFindBounds(image);
            return image.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var picture in pictures)
                if (picture != null && picture.Image != null) { picture.Image.Dispose(); picture.Image = null; }
        base.Dispose(disposing);
    }
}
