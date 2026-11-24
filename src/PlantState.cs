using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

internal sealed partial class PlantState
{
    internal const int MinimumZoom = 60, MaximumZoom = 180;
    internal const int RichMoisture = 65, DryMoisture = 25;
    internal const double HoursToMaturity = 6;
    private const double MoistureLossPerHour = 4;
    private const double StartingMoisture = 45;
    private static readonly CultureInfo DataCulture = CultureInfo.InvariantCulture;
    private readonly string path;
    private readonly Dictionary<PlantSpecies, int> garden = new Dictionary<PlantSpecies, int>();
    private readonly List<OrchardPlot> orchard = new List<OrchardPlot>();
    private readonly Dictionary<PlantSpecies, long> fruit = new Dictionary<PlantSpecies, long>();
    private double moisture = StartingMoisture;
    private double growthHours;
    private DateTime measuredAt = DateTime.UtcNow;

    internal Point? Position { get; private set; }
    internal int Zoom { get; private set; }
    internal PlantSpecies Active { get; private set; }
    internal bool MotionEnabled { get; private set; }
    internal bool AutoHarvestEnabled { get; private set; }
    internal IReadOnlyList<OrchardPlot> Plots { get; private set; }

    internal PlantState(string path)
    {
        this.path = path;
        Zoom = 100;
        Active = PlantCatalog.Jade;
        MotionEnabled = true;
        WaterReminderEnabled = HarvestReminderEnabled = true;
        Plots = orchard.AsReadOnly();
        RecycleBin = recycled.AsReadOnly();
        var currentVersion = Load();
        var now = DateTime.UtcNow;
        if (!Owns(equippedPot)) equippedPot = null;
        if (!Owns(equippedCan)) equippedCan = null;
        var recoveredJournal = RecoverJournal(now);
        if (currentVersion)
        {
            if (!PurgeExpired(now) && recoveredJournal) Save();
        }
        else
        {
            recycled.RemoveAll(entry => entry.IsExpired(now));
            Save();
        }
    }

    private bool Load()
    {
        try
        {
            var fields = File.ReadAllLines(path);
            double storedMoisture;
            long ticks;
            if (fields.Length < 3 || (fields[0] != "1" && fields[0] != "2" && fields[0] != "3" && fields[0] != "4" && fields[0] != "5" && fields[0] != "6") ||
                !TryReadNumber(fields[1], out storedMoisture) ||
                !long.TryParse(fields[2], NumberStyles.None, DataCulture, out ticks) ||
                ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return false;

            moisture = Math.Max(0, Math.Min(100, storedMoisture));
            measuredAt = new DateTime(ticks, DateTimeKind.Utc);
            int x, y;
            if (fields.Length >= 5 &&
                int.TryParse(fields[3], NumberStyles.Integer, DataCulture, out x) &&
                int.TryParse(fields[4], NumberStyles.Integer, DataCulture, out y))
            {
                Position = new Point(x, y);
            }

            if (fields[0] == "1")
            {
                // Preserve water using the old decay rate up to the upgrade, then start growing.
                var now = DateTime.UtcNow;
                moisture = Math.Max(0, moisture - ElapsedHours(now));
                measuredAt = now;
                return false;
            }

            double storedGrowth;
            if (fields.Length >= 6 && TryReadNumber(fields[5], out storedGrowth))
                growthHours = Math.Max(0, Math.Min(HoursToMaturity, storedGrowth));
            int storedZoom;
            if (fields.Length >= 7 && int.TryParse(fields[6], NumberStyles.Integer, DataCulture, out storedZoom))
                Zoom = ClampZoom(storedZoom);
            if (fields[0] == "2") return false;

            if (fields.Length >= 8) Active = PlantCatalog.Find(fields[7]);
            var migrationTime = DateTime.UtcNow;
            var storedPlots = new List<string>();
            for (int i = 8; i < fields.Length; i++)
            {
                var parts = fields[i].Split('=');
                if (parts.Length != 2) continue;
                if (ReadCollectionField(parts[0], parts[1])) continue;
                if (parts[0] == "motion")
                {
                    MotionEnabled = parts[1] != "0";
                    continue;
                }
                if (parts[0] == "orchard-auto")
                {
                    AutoHarvestEnabled = parts[1] == "1";
                    continue;
                }
                if (parts[0] == "plot")
                {
                    storedPlots.Add(parts[1]);
                    continue;
                }
                if (parts[0] == "recycle")
                {
                    ReadRecycled(parts[1]);
                    continue;
                }
                if (parts[0].StartsWith("fruit.", StringComparison.Ordinal))
                {
                    var crop = PlantCatalog.Find(parts[0].Substring(6));
                    long quantity;
                    if (crop != null && crop.IsCrop && long.TryParse(parts[1], NumberStyles.None, DataCulture, out quantity) && quantity > 0)
                        fruit[crop] = quantity;
                    continue;
                }
                var species = PlantCatalog.Find(parts[0]);
                int count;
                if (species != null && int.TryParse(parts[1], NumberStyles.None, DataCulture, out count) && count > 0)
                    garden[species] = count;
            }
            foreach (var plot in storedPlots) ReadPlot(plot, migrationTime);
            return fields[0] == "6";
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return false;
    }

    private void ReadPlot(string value, DateTime now)
    {
        var parts = value.Split(',');
        if (parts.Length != 2 && parts.Length != 4) return;
        var crop = PlantCatalog.Find(parts[0]);
        long ticks;
        if (crop == null || !crop.IsCrop || !long.TryParse(parts[1], NumberStyles.None, DataCulture, out ticks) ||
            ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return;
        var next = new DateTime(ticks, DateTimeKind.Utc);
        int ripe = 0, slot = 0;
        if (parts.Length == 2 && next <= now)
        {
            var oldCycle = TimeSpan.FromMinutes(crop.HarvestMinutes).Ticks;
            var oldBatches = 1 + (now.Ticks - next.Ticks) / oldCycle;
            var earned = oldBatches * crop.PreviousBatchYield;
            var stored = FruitOf(crop);
            if (AutoHarvestEnabled && earned <= long.MaxValue - stored)
            {
                // Credit the automatic harvests due under the old rules before switching schedules.
                fruit[crop] = stored + earned;
                next = OrchardPlot.Offset(next, oldBatches * oldCycle);
            }
            else
            {
                ripe = crop.PreviousBatchYield;
                slot = ripe % crop.FruitCapacity;
                next = OrchardPlot.Offset(now, crop.FruitIntervalTicks);
            }
        }
        if (parts.Length == 4 &&
            (!int.TryParse(parts[2], NumberStyles.None, DataCulture, out ripe) || ripe > crop.FruitCapacity ||
             !int.TryParse(parts[3], NumberStyles.None, DataCulture, out slot) || slot >= crop.FruitCapacity)) return;
        orchard.Add(new OrchardPlot(crop, new FruitSchedule(ripe, slot, next)));
    }

    private static bool TryReadNumber(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, DataCulture, out number) &&
               !double.IsNaN(number) && !double.IsInfinity(number);
    }

    private double ElapsedHours(DateTime now) { return Math.Max(0, (now - measuredAt).TotalHours); }

    internal PlantReading Read(DateTime now)
    {
        if (Active == null) return new PlantReading(null, 0, 0);
        var hours = ElapsedHours(now);
        var richHours = Math.Min(hours, Math.Max(0, (moisture - RichMoisture) / MoistureLossPerHour));
        var ordinaryHours = Math.Min(hours - richHours, Math.Max(0, (moisture - DryMoisture) / MoistureLossPerHour - richHours));
        var dryHours = hours - richHours - ordinaryHours;
        var accumulated = Math.Min(HoursToMaturity, growthHours + richHours * 2 + ordinaryHours + dryHours * .5);
        return new PlantReading(Active, Math.Max(0, moisture - hours * MoistureLossPerHour), accumulated / HoursToMaturity * 100);
    }

    internal bool Water(DateTime now)
    {
        var reading = Read(now);
        if (Active == null || reading.MoisturePercent == 100) return false;
        moisture = Math.Min(100, reading.Moisture + 25);
        growthHours = reading.Progress / 100 * HoursToMaturity;
        if (now > measuredAt) measuredAt = now;
        Save();
        return true;
    }

    internal static int ClampZoom(int zoom) { return Math.Max(MinimumZoom, Math.Min(MaximumZoom, zoom)); }

    internal int CountOf(PlantSpecies plant)
    {
        int count;
        return garden.TryGetValue(plant, out count) ? count : 0;
    }

    internal bool Archive(DateTime now)
    {
        var reading = Read(now);
        if (!reading.Mature) return false;
        var species = Active;
        if (species.IsCrop)
        {
            orchard.Add(new OrchardPlot(species, new FruitSchedule(1, 1, OrchardPlot.Offset(now, species.FruitIntervalTicks))));
            Active = null;
            if (SaveWithJournal(species, reading.Stage)) return true;
            orchard.RemoveAt(orchard.Count - 1);
            Active = species;
            return false;
        }
        var count = CountOf(species);
        if (count == int.MaxValue) return false;
        Active = null;
        garden[species] = count + 1;
        if (SaveWithJournal(species, reading.Stage)) return true;

        Active = species;
        if (count == 0) garden.Remove(species);
        else garden[species] = count;
        return false;
    }

    internal bool Sow(PlantSpecies species, DateTime now)
    {
        if (Active != null || species == null) return false;
        Active = species;
        moisture = StartingMoisture;
        growthHours = 0;
        measuredAt = now;
        if (SaveWithJournal(species, 0)) return true;
        Active = null;
        return false;
    }

    internal void RememberView(Point position, int zoom)
    {
        if (Position == position && Zoom == zoom) return;
        Position = position;
        Zoom = ClampZoom(zoom);
        Save();
    }

    internal bool SetMotion(bool enabled)
    {
        if (enabled == MotionEnabled) return true;
        MotionEnabled = enabled;
        if (Save()) return true;
        MotionEnabled = !enabled;
        return false;
    }

    internal long FruitOf(PlantSpecies crop)
    {
        long quantity;
        return fruit.TryGetValue(crop, out quantity) ? quantity : 0;
    }

    internal bool SetAutoHarvest(bool enabled, DateTime now)
    {
        if (AutoHarvestEnabled == enabled) return true;
        var reset = new Dictionary<OrchardPlot, FruitSchedule>();
        if (enabled)
        {
            // Restart the automatic timeline when a full plot has been waiting in manual mode.
            foreach (var plot in orchard)
            {
                var current = plot.Read(now);
                if (current.Ripe == plot.Crop.FruitCapacity)
                {
                    reset.Add(plot, plot.Schedule);
                    plot.Schedule = new FruitSchedule(current.Ripe, current.NextSlot, now);
                }
            }
        }
        AutoHarvestEnabled = enabled;
        if (Save()) return true;
        AutoHarvestEnabled = !enabled;
        foreach (var entry in reset) entry.Key.Schedule = entry.Value;
        return false;
    }

    internal List<HarvestPick> Harvest(DateTime now, OrchardPlot only, bool automatic)
    {
        var picks = new List<HarvestPick>();
        if (automatic && !AutoHarvestEnabled) return picks;
        foreach (var plot in orchard)
        {
            if (only != null && plot != only) continue;
            var current = plot.Read(now);
            var capacity = plot.Crop.FruitCapacity;
            var interval = plot.Crop.FruitIntervalTicks;
            bool full = current.Ripe == capacity;
            if (current.Ripe == 0 || (automatic && (!full || current.NextAt > now))) continue;
            var next = full ? OrchardPlot.Offset(now, interval) : current.NextAt;
            long quantity = current.Ripe;
            if (automatic)
            {
                long cycle = interval * capacity;
                long completedAfterFirst = (now.Ticks - current.NextAt.Ticks) / cycle;
                quantity = (completedAfterFirst + 1) * capacity;
                next = OrchardPlot.Offset(current.NextAt, completedAfterFirst * cycle + interval);
            }
            var stored = FruitOf(plot.Crop);
            if (quantity > long.MaxValue - stored) continue;
            picks.Add(new HarvestPick(plot, quantity, current));
            fruit[plot.Crop] = stored + quantity;
            plot.Schedule = new FruitSchedule(0, current.NextSlot, next);
        }
        if (picks.Count == 0 || Save()) return picks;
        foreach (var pick in picks)
        {
            pick.Plot.Schedule = pick.PreviousSchedule;
            var quantity = FruitOf(pick.Plot.Crop) - pick.Quantity;
            if (quantity == 0) fruit.Remove(pick.Plot.Crop);
            else fruit[pick.Plot.Crop] = quantity;
        }
        return null;
    }

    private bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var pending = path + ".pending";
            var fields = new List<string> {
                "6", moisture.ToString("R", DataCulture), measuredAt.Ticks.ToString(DataCulture),
                Position.HasValue ? Position.Value.X.ToString(DataCulture) : string.Empty,
                Position.HasValue ? Position.Value.Y.ToString(DataCulture) : string.Empty,
                growthHours.ToString("R", DataCulture), Zoom.ToString(DataCulture),
                Active == null ? string.Empty : Active.Id
            };
            fields.Add(MotionEnabled ? "motion=1" : "motion=0");
            fields.Add(AutoHarvestEnabled ? "orchard-auto=1" : "orchard-auto=0");
            AddCollectionFields(fields);
            foreach (var plot in orchard)
                fields.Add("plot=" + plot.Crop.Id + "," + plot.Schedule.NextAt.Ticks.ToString(DataCulture) + "," +
                           plot.Schedule.Ripe.ToString(DataCulture) + "," + plot.Schedule.NextSlot.ToString(DataCulture));
            foreach (var entry in recycled) fields.Add("recycle=" + WriteRecycled(entry));
            foreach (var species in PlantCatalog.All)
            {
                var count = CountOf(species);
                if (count != 0) fields.Add(species.Id + "=" + count.ToString(DataCulture));
                var quantity = FruitOf(species);
                if (quantity != 0) fields.Add("fruit." + species.Id + "=" + quantity.ToString(DataCulture));
            }
            File.WriteAllLines(pending, fields);
            if (File.Exists(path)) File.Replace(pending, path, null);
            else File.Move(pending, path);
            return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return false;
    }
}

internal struct PlantReading
{
    internal readonly PlantSpecies Species;
    internal readonly double Moisture, Progress;

    internal PlantReading(PlantSpecies species, double moisture, double progress)
    {
        Species = species;
        Moisture = moisture;
        Progress = progress;
    }

    internal int MoisturePercent { get { return (int)Math.Round(Moisture); } }
    internal bool Mature { get { return Species != null && Progress >= 100; } }
    internal int Stage { get { return Species == null ? 0 : Species.StageAt(Progress); } }
    internal string StageName { get { return Species == null ? string.Empty : Species.StageNames[Stage]; } }
    internal string ProgressText { get { return (Math.Floor(Progress * 10) / 10).ToString("0.0", CultureInfo.CurrentCulture) + "%"; } }
    internal double GrowthSpeed { get { return Moisture >= PlantState.RichMoisture ? 2 : Moisture >= PlantState.DryMoisture ? 1 : .5; } }
}
