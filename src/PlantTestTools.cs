#if DESKPLANT_TEST
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed partial class PlantState
{
    internal bool TestPlaceDesktop(PlantSpecies species, int stage, DateTime now)
    {
        if (species == null || stage < 0 || stage > species.FinalStage) return false;
        return SaveTransfer(delegate
        {
            if (Active != null) PutDesktopInBin(now);
            Active = species;
            moisture = StartingMoisture;
            // Use the middle of a stage so floating point roundoff can't select the previous stage.
            var progress = stage == species.FinalStage ? 100 : (species.Milestones[stage] + species.Milestones[stage + 1]) * .5;
            growthHours = stage == 0 ? 0 : HoursToMaturity * progress / 100;
            measuredAt = now;
            Unlock(species, stage);
        });
    }

    internal bool TestAddGarden(PlantSpecies species, int quantity)
    {
        if (species == null || species.IsCrop || quantity <= 0 || CountOf(species) > int.MaxValue - quantity) return false;
        return SaveTransfer(delegate
        {
            garden[species] = CountOf(species) + quantity;
            Unlock(species, species.FinalStage);
        });
    }

    internal bool TestAddPlots(PlantSpecies species, int quantity, bool ready, DateTime now)
    {
        if (species == null || !species.IsCrop || quantity <= 0) return false;
        var schedule = ready ? new FruitSchedule(species.FruitCapacity, 0, now) :
            new FruitSchedule(0, 0, OrchardPlot.Offset(now, species.FruitIntervalTicks));
        return SaveTransfer(delegate
        {
            for (int i = 0; i < quantity; i++) orchard.Add(new OrchardPlot(species, schedule));
            Unlock(species, species.FinalStage);
        });
    }

    internal bool TestChangeFruit(PlantSpecies species, int difference)
    {
        if (species == null || !species.IsCrop) return false;
        var before = FruitOf(species);
        var after = difference > 0 ? before + Math.Min(long.MaxValue - before, difference) : Math.Max(0, before + difference);
        if (after == 0) fruit.Remove(species);
        else fruit[species] = after;
        if (after > before ? SaveWithJournal(species, species.FinalStage) : Save()) return true;
        if (before == 0) fruit.Remove(species);
        else fruit[species] = before;
        return false;
    }

    internal bool TestRipen(DateTime now, bool full)
    {
        var prior = orchard.Select(plot => plot.Schedule).ToArray();
        foreach (var plot in orchard)
        {
            var capacity = plot.Crop.FruitCapacity;
            var current = plot.Read(now);
            if (current.Ripe >= capacity) continue;
            var ripe = full ? capacity : current.Ripe + 1;
            plot.Schedule = new FruitSchedule(ripe, (current.NextSlot + ripe - current.Ripe) % capacity,
                ripe == capacity ? now : OrchardPlot.Offset(now, plot.Crop.FruitIntervalTicks));
        }
        if (Save()) return true;
        for (int i = 0; i < prior.Length; i++) orchard[i].Schedule = prior[i];
        return false;
    }

    internal bool TestUprootGarden(PlantSpecies species, int quantity, DateTime now)
    {
        var before = CountOf(species);
        var amount = Math.Min(quantity, before);
        if (amount <= 0) return false;
        return SaveTransfer(delegate
        {
            if (before == amount) garden.Remove(species);
            else garden[species] = before - amount;
            for (int i = 0; i < amount; i++) recycled.Add(new RecycledPlant(species, PlantLocation.Garden, now));
        });
    }

    internal bool TestUprootPlots(PlantSpecies species, int quantity, DateTime now)
    {
        var picks = Enumerable.Range(0, orchard.Count).Where(index => orchard[index].Crop == species).Take(quantity).ToArray();
        if (picks.Length == 0) return false;
        return SaveTransfer(delegate
        {
            for (int i = picks.Length - 1; i >= 0; i--)
            {
                var index = picks[i];
                recycled.Add(SnapshotPlot(orchard[index], index, now));
                orchard.RemoveAt(index);
            }
        });
    }
}
#endif
