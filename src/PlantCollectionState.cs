using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal enum DecorationKind { Pot, WateringCan }

internal sealed class Decoration
{
    internal readonly string Id, Name, Resource;
    internal readonly DecorationKind Kind;
    internal readonly int Price;

    private Decoration(string id, string name, DecorationKind kind, int price, string resource)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Price = price;
        Resource = resource;
    }

    internal static readonly Decoration[] All = {
        new Decoration("original-pot", "原配花盆", DecorationKind.Pot, 0, null),
        new Decoration("blue-porcelain", "青花瓷盆", DecorationKind.Pot, 48, "DeskPlant.Shop.Porcelain"),
        new Decoration("carved-clay", "藤纹陶盆", DecorationKind.Pot, 64, "DeskPlant.Shop.Clay"),
        new Decoration("original-can", "蓝色水壶", DecorationKind.WateringCan, 0, "DeskPlant.WateringCan"),
        new Decoration("brass-can", "黄铜水壶", DecorationKind.WateringCan, 72, "DeskPlant.Shop.Brass"),
        new Decoration("mint-can", "薄荷珐琅水壶", DecorationKind.WateringCan, 88, "DeskPlant.Shop.Mint")
    };

    internal static Decoration Find(string id) { return Array.Find(All, item => item.Id == id); }
    internal static Decoration Default(DecorationKind kind) { return Array.Find(All, item => item.Kind == kind && item.Price == 0); }
}

internal sealed partial class PlantState
{
    private readonly Dictionary<PlantSpecies, int> journal = new Dictionary<PlantSpecies, int>();
    private readonly HashSet<Decoration> owned = new HashSet<Decoration>();
    private Decoration equippedPot, equippedCan;

    internal long Coins { get; private set; }
    internal bool WaterReminderEnabled { get; private set; }
    internal bool HarvestReminderEnabled { get; private set; }

    internal Decoration Equipped(DecorationKind kind)
    {
        return (kind == DecorationKind.Pot ? equippedPot : equippedCan) ?? Decoration.Default(kind);
    }

    internal bool Owns(Decoration item) { return item != null && (item.Price == 0 || owned.Contains(item)); }

    internal bool Purchase(Decoration item)
    {
        if (item == null || Owns(item) || Coins < item.Price) return false;
        Coins -= item.Price;
        owned.Add(item);
        if (Save()) return true;
        owned.Remove(item);
        Coins += item.Price;
        return false;
    }

    internal bool Equip(Decoration item)
    {
        if (!Owns(item)) return false;
        var previous = Equipped(item.Kind);
        if (previous == item) return true;
        SetEquipped(item);
        if (Save()) return true;
        SetEquipped(previous);
        return false;
    }

    private void SetEquipped(Decoration item)
    {
        if (item.Kind == DecorationKind.Pot) equippedPot = item;
        else equippedCan = item;
    }

    internal static int FruitValue(PlantSpecies crop)
    {
        if (crop == null || !crop.IsCrop) return 0;
        switch (crop.Id)
        {
            case "pineapple": return 9;
            case "passion-fruit": return 8;
            case "kumquat": return 7;
            case "strawberry": return 6;
            case "cherry-tomato": return 5;
            default: return 4;
        }
    }

    internal long SaleLimit(PlantSpecies crop)
    {
        var value = FruitValue(crop);
        return value == 0 ? 0 : Math.Min(FruitOf(crop), (long.MaxValue - Coins) / value);
    }

    internal bool SellFruit(PlantSpecies crop, long quantity)
    {
        if (quantity <= 0 || quantity > SaleLimit(crop)) return false;
        var previous = FruitOf(crop);
        var earned = quantity * FruitValue(crop);
        if (quantity == previous) fruit.Remove(crop);
        else fruit[crop] = previous - quantity;
        Coins += earned;
        if (Save()) return true;
        Coins -= earned;
        fruit[crop] = previous;
        return false;
    }

    internal bool SetReminder(bool watering, bool enabled)
    {
        var previous = watering ? WaterReminderEnabled : HarvestReminderEnabled;
        if (previous == enabled) return true;
        if (watering) WaterReminderEnabled = enabled;
        else HarvestReminderEnabled = enabled;
        if (Save()) return true;
        if (watering) WaterReminderEnabled = previous;
        else HarvestReminderEnabled = previous;
        return false;
    }

    internal int JournalStage(PlantSpecies plant)
    {
        int stage;
        return journal.TryGetValue(plant, out stage) ? stage : -1;
    }

    internal void RecordGrowth(DateTime now)
    {
        var reading = Read(now);
        if (reading.Species == null) return;
        if (reading.Stage > JournalStage(reading.Species)) SaveWithJournal(reading.Species, reading.Stage);
    }

    private bool SaveWithJournal(PlantSpecies species, int stage)
    {
        var previous = JournalStage(species);
        bool changed = Unlock(species, stage);
        if (Save()) return true;
        if (changed && previous < 0) journal.Remove(species);
        else if (changed) journal[species] = previous;
        return false;
    }

    private bool Unlock(PlantSpecies plant, int stage)
    {
        if (stage <= JournalStage(plant)) return false;
        journal[plant] = stage;
        return true;
    }

    private bool RecoverJournal(DateTime now)
    {
        bool changed = false;
        if (Active != null) changed |= Unlock(Active, Read(now).Stage);
        foreach (var species in PlantCatalog.All)
            if (CountOf(species) > 0 || FruitOf(species) > 0) changed |= Unlock(species, species.FinalStage);
        foreach (var plot in orchard) changed |= Unlock(plot.Crop, plot.Crop.FinalStage);
        foreach (var entry in recycled)
            changed |= Unlock(entry.Species, entry.Location == PlantLocation.Desktop ? entry.Species.StageAt(entry.Progress) : entry.Species.FinalStage);
        return changed;
    }

    private bool ReadCollectionField(string key, string value)
    {
        if (key == "coins")
        {
            long coins;
            if (long.TryParse(value, NumberStyles.None, DataCulture, out coins)) Coins = coins;
        }
        else if (key == "owned")
        {
            foreach (var id in value.Split(','))
            {
                var item = Decoration.Find(id);
                if (item != null && item.Price > 0) owned.Add(item);
            }
        }
        else if (key == "pot" || key == "watering-can")
        {
            var item = Decoration.Find(value);
            if (item != null && (item.Kind == DecorationKind.Pot) == (key == "pot")) SetEquipped(item);
        }
        else if (key == "remind-water") WaterReminderEnabled = value != "0";
        else if (key == "remind-harvest") HarvestReminderEnabled = value != "0";
        else if (key.StartsWith("journal.", StringComparison.Ordinal))
        {
            var plant = PlantCatalog.Find(key.Substring(8));
            int stage;
            if (plant != null && int.TryParse(value, NumberStyles.None, DataCulture, out stage)) Unlock(plant, Math.Min(stage, plant.FinalStage));
        }
        else return false;
        return true;
    }

    private void AddCollectionFields(List<string> fields)
    {
        fields.Add("coins=" + Coins.ToString(DataCulture));
        fields.Add("owned=" + string.Join(",", Decoration.All.Where(owned.Contains).Select(item => item.Id)));
        fields.Add("pot=" + Equipped(DecorationKind.Pot).Id);
        fields.Add("watering-can=" + Equipped(DecorationKind.WateringCan).Id);
        fields.Add(WaterReminderEnabled ? "remind-water=1" : "remind-water=0");
        fields.Add(HarvestReminderEnabled ? "remind-harvest=1" : "remind-harvest=0");
        foreach (var plant in PlantCatalog.All)
        {
            var stage = JournalStage(plant);
            if (stage >= 0) fields.Add("journal." + plant.Id + "=" + stage.ToString(DataCulture));
        }
    }
}
