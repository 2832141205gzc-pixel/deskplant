using System;

internal sealed class OrchardPlot
{
    internal readonly PlantSpecies Crop;
    internal FruitSchedule Schedule;

    internal OrchardPlot(PlantSpecies crop, FruitSchedule schedule)
    {
        Crop = crop;
        Schedule = schedule;
    }

    internal FruitSchedule Read(DateTime now)
    {
        var capacity = Crop.FruitCapacity;
        var current = Schedule;
        if (current.Ripe == capacity || now < current.NextAt) return current;
        var interval = Crop.FruitIntervalTicks;
        int grown = (int)Math.Min(capacity - current.Ripe, 1 + (now.Ticks - current.NextAt.Ticks) / interval);
        int ripe = current.Ripe + grown;
        // While full, the stored/read time identifies when the cap was reached.
        var next = Offset(current.NextAt, interval * (ripe == capacity ? grown - 1 : grown));
        return new FruitSchedule(ripe, (current.NextSlot + grown) % capacity, next);
    }

    internal static DateTime Offset(DateTime time, long ticks)
    {
        return new DateTime(Math.Min(DateTime.MaxValue.Ticks, time.Ticks + ticks), DateTimeKind.Utc);
    }
}

internal struct FruitSchedule
{
    internal readonly int Ripe, NextSlot;
    internal readonly DateTime NextAt;

    internal FruitSchedule(int ripe, int nextSlot, DateTime nextAt)
    {
        Ripe = ripe;
        NextSlot = nextSlot;
        NextAt = nextAt;
    }

    internal int RipeSlot(int index, int capacity) { return (NextSlot + capacity - Ripe + index) % capacity; }
}

internal sealed class HarvestPick
{
    internal readonly OrchardPlot Plot;
    internal readonly long Quantity;
    internal readonly FruitSchedule PreviousSchedule, Picked;

    internal HarvestPick(OrchardPlot plot, long quantity, FruitSchedule picked)
    {
        Plot = plot;
        Quantity = quantity;
        PreviousSchedule = plot.Schedule;
        Picked = picked;
    }
}
