using System;
using System.Collections.Generic;
using System.Globalization;

internal enum PlantLocation { Desktop, Garden, Orchard }

internal sealed class RecycledPlant
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    internal readonly PlantSpecies Species;
    internal readonly PlantLocation Location;
    internal readonly DateTime RemovedAt;
    internal readonly double Moisture, GrowthHours;
    internal readonly long RemainingTicks;
    internal readonly int PlotIndex, RipeFruit, NextFruitSlot;
    internal double Progress { get { return GrowthHours / PlantState.HoursToMaturity * 100; } }

    internal RecycledPlant(PlantSpecies species, PlantLocation location, DateTime removedAt,
                           double moisture = 0, double growthHours = 0, long remainingTicks = 0, int plotIndex = 0,
                           int ripeFruit = 0, int nextFruitSlot = 0)
    {
        Species = species;
        Location = location;
        RemovedAt = removedAt;
        Moisture = moisture;
        GrowthHours = growthHours;
        RemainingTicks = remainingTicks;
        PlotIndex = plotIndex;
        RipeFruit = ripeFruit;
        NextFruitSlot = nextFruitSlot;
    }

    internal DateTime ExpiresAt
    {
        get { return new DateTime(Math.Min(DateTime.MaxValue.Ticks, RemovedAt.Ticks + Retention.Ticks), DateTimeKind.Utc); }
    }

    internal bool IsExpired(DateTime now) { return now >= ExpiresAt; }
}

internal sealed partial class PlantState
{
    private readonly List<RecycledPlant> recycled = new List<RecycledPlant>();
    internal IReadOnlyList<RecycledPlant> RecycleBin { get; private set; }

    internal bool UprootDesktop(DateTime now)
    {
        if (Active == null) return false;
        RecordGrowth(now);
        return SaveTransfer(delegate { PutDesktopInBin(now); });
    }

    internal bool UprootGarden(PlantSpecies species, DateTime now)
    {
        var count = CountOf(species);
        if (count == 0) return false;
        return SaveTransfer(delegate
        {
            if (count == 1) garden.Remove(species);
            else garden[species] = count - 1;
            recycled.Add(new RecycledPlant(species, PlantLocation.Garden, now));
        });
    }

    internal bool UprootPlot(OrchardPlot plot, DateTime now)
    {
        var index = orchard.IndexOf(plot);
        if (index < 0) return false;
        return SaveTransfer(delegate
        {
            recycled.Add(SnapshotPlot(plot, index, now));
            orchard.RemoveAt(index);
        });
    }

    internal bool Restore(RecycledPlant entry, DateTime now, bool exchangeDesktop)
    {
        if (entry == null || entry.IsExpired(now) || !recycled.Contains(entry)) return false;
        if (entry.Location == PlantLocation.Desktop && Active != null && !exchangeDesktop) return false;
        if (entry.Location == PlantLocation.Garden && CountOf(entry.Species) == int.MaxValue) return false;

        return SaveTransfer(delegate
        {
            switch (entry.Location)
            {
                case PlantLocation.Desktop:
                    if (Active != null) PutDesktopInBin(now);
                    Active = entry.Species;
                    moisture = entry.Moisture;
                    growthHours = entry.GrowthHours;
                    measuredAt = now;
                    break;
                case PlantLocation.Garden:
                    garden[entry.Species] = CountOf(entry.Species) + 1;
                    break;
                case PlantLocation.Orchard:
                    var ready = OrchardPlot.Offset(now, entry.RemainingTicks);
                    orchard.Insert(Math.Min(orchard.Count, entry.PlotIndex), new OrchardPlot(entry.Species,
                        new FruitSchedule(entry.RipeFruit, entry.NextFruitSlot, ready)));
                    break;
            }
            recycled.Remove(entry);
        });
    }

    internal bool PurgeExpired(DateTime now)
    {
        if (!recycled.Exists(entry => entry.IsExpired(now))) return false;
        return SaveTransfer(delegate { recycled.RemoveAll(entry => entry.IsExpired(now)); });
    }

    private void PutDesktopInBin(DateTime now)
    {
        var reading = Read(now);
        Unlock(Active, reading.Stage);
        recycled.Add(new RecycledPlant(Active, PlantLocation.Desktop, now, reading.Moisture, reading.Progress / 100 * HoursToMaturity));
        Active = null;
    }

    private static RecycledPlant SnapshotPlot(OrchardPlot plot, int index, DateTime now)
    {
        var current = plot.Read(now);
        var remaining = current.Ripe == plot.Crop.FruitCapacity ? 0 : Math.Max(0, (current.NextAt - now).Ticks);
        return new RecycledPlant(plot.Crop, PlantLocation.Orchard, now, remainingTicks: remaining, plotIndex: index,
            ripeFruit: current.Ripe, nextFruitSlot: current.NextSlot);
    }

    private bool SaveTransfer(Action transfer)
    {
        var active = Active;
        var formerMoisture = moisture;
        var formerGrowth = growthHours;
        var formerTime = measuredAt;
        var formerGarden = new Dictionary<PlantSpecies, int>(garden);
        var formerJournal = new Dictionary<PlantSpecies, int>(journal);
        var formerOrchard = orchard.ToArray();
        var formerBin = recycled.ToArray();
        transfer();
        if (Save()) return true;

        Active = active;
        moisture = formerMoisture;
        growthHours = formerGrowth;
        measuredAt = formerTime;
        garden.Clear();
        foreach (var pair in formerGarden) garden.Add(pair.Key, pair.Value);
        journal.Clear();
        foreach (var pair in formerJournal) journal.Add(pair.Key, pair.Value);
        orchard.Clear();
        orchard.AddRange(formerOrchard);
        recycled.Clear();
        recycled.AddRange(formerBin);
        return false;
    }

    private void ReadRecycled(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 7 && parts.Length != 9) return;
        var species = PlantCatalog.Find(parts[0]);
        PlantLocation location;
        long removed, remaining;
        int index;
        double water, hours;
        if (species == null || !Enum.TryParse(parts[1], out location) || !Enum.IsDefined(typeof(PlantLocation), location) ||
            (location == PlantLocation.Orchard && !species.IsCrop) ||
            !long.TryParse(parts[2], NumberStyles.None, DataCulture, out removed) || removed <= 0 || removed > DateTime.MaxValue.Ticks ||
            !TryReadNumber(parts[3], out water) || !TryReadNumber(parts[4], out hours) ||
            !long.TryParse(parts[5], NumberStyles.None, DataCulture, out remaining) || remaining > DateTime.MaxValue.Ticks ||
            !int.TryParse(parts[6], NumberStyles.None, DataCulture, out index)) return;
        int ripe = 0, slot = 0;
        if (location == PlantLocation.Orchard)
        {
            if (parts.Length == 7 && remaining == 0)
            {
                ripe = species.PreviousBatchYield;
                slot = ripe % species.FruitCapacity;
                remaining = species.FruitIntervalTicks;
            }
            if (parts.Length == 9 &&
                (!int.TryParse(parts[7], NumberStyles.None, DataCulture, out ripe) || ripe > species.FruitCapacity ||
                 !int.TryParse(parts[8], NumberStyles.None, DataCulture, out slot) || slot >= species.FruitCapacity)) return;
        }
        recycled.Add(new RecycledPlant(species, location, new DateTime(removed, DateTimeKind.Utc),
            Math.Max(0, Math.Min(100, water)), Math.Max(0, Math.Min(HoursToMaturity, hours)), remaining, index, ripe, slot));
    }

    private static string WriteRecycled(RecycledPlant entry)
    {
        return string.Join(",", entry.Species.Id, entry.Location.ToString(), entry.RemovedAt.Ticks.ToString(DataCulture),
            entry.Moisture.ToString("R", DataCulture), entry.GrowthHours.ToString("R", DataCulture),
            entry.RemainingTicks.ToString(DataCulture), entry.PlotIndex.ToString(DataCulture),
            entry.RipeFruit.ToString(DataCulture), entry.NextFruitSlot.ToString(DataCulture));
    }
}
