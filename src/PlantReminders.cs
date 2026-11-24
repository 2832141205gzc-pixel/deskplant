using System;
using System.Collections.Generic;

internal sealed class PlantReminders
{
    private PlantSpecies warnedDry;
    private readonly HashSet<OrchardPlot> warnedRipe = new HashSet<OrchardPlot>();
    private DateTime nextHarvestAlert;

    internal string Check(PlantState state, DateTime now, out bool openOrchard)
    {
        var reading = state.Read(now);
        var dry = state.WaterReminderEnabled && reading.Species != null && !reading.Mature && reading.Moisture < PlantState.DryMoisture;
        var water = dry && reading.Species != warnedDry;
        warnedDry = dry ? reading.Species : null;

        var ready = new HashSet<OrchardPlot>();
        if (state.HarvestReminderEnabled && !state.AutoHarvestEnabled)
            foreach (var plot in state.Plots)
                if (plot.Read(now).Ripe > 0) ready.Add(plot);
        warnedRipe.IntersectWith(ready);
        openOrchard = ready.Count > warnedRipe.Count && now >= nextHarvestAlert;
        if (openOrchard)
        {
            warnedRipe.UnionWith(ready);
            nextHarvestAlert = now.AddMinutes(2);
        }

        var message = water ? reading.Species.Name + "的水分低于 25%，可以浇水了。" : "";
        if (openOrchard) message += (water ? "\n" : "") + "果园有 " + ready.Count + " 块地可以采摘。";
        return message;
    }
}
