using System.Text.Json.Nodes;
using Tandur.Core;

namespace Tandur.World
{
    public sealed record CropDef(string Id, string Name, string Seed, string Produce, int[] StageDays, int? RegrowDays)
    {
        public int FinalStage => StageDays.Length;

        public static Dictionary<string, CropDef> Load(string path)
        {
            var d = new Dictionary<string, CropDef>();
            foreach (var (k, v) in JsonNode.Parse(File.ReadAllText(path))!.AsObject())
                d[k] = new CropDef(k, (string)v!["name"]!, (string)v["seed"]!, (string)v["produce"]!,
                                   v["stage_days"]!.AsArray().Select(x => J.I(x)).ToArray(), J.In(v["regrow_days"]));
            return d;
        }
    }

    /// <summary>Satu petak tanah yang sudah dicangkul. ENKAPSULASI: hanya FarmField yang mengubah isinya.</summary>
    public sealed class Plot
    {
        public bool Watered { get; internal set; }
        public string? Crop { get; internal set; }
        public int Stage { get; internal set; }
        public int Progress { get; internal set; }
    }

    public enum ActionKind { Till, Water, Plant, Harvest, None }

    public sealed record ActionResult(ActionKind Kind, bool Ok, string Message = "", string? Item = null);

    public enum Preview { Ok, Bad, Info }

    /// <summary>Aturan bertani: mencangkul, menyiram, menanam, tumbuh, panen. Logika murni tanpa grafis.</summary>
    public sealed class FarmField
    {
        private readonly WorldMap map;

        public FarmField(WorldMap map, Dictionary<string, CropDef> crops)
        {
            this.map = map;
            Crops = crops;
        }

        public Dictionary<string, CropDef> Crops { get; }
        public Dictionary<(int X, int Y), Plot> Plots { get; } = new();
        public WorldMap Map => map;

        public bool IsFarmable(int x, int y) => map.KindAt(x, y) == "farmland" && !map.Blockers.Contains((x, y));

        public bool IsRipe(int x, int y)
            => Plots.TryGetValue((x, y), out var p) && p.Crop != null && p.Stage >= Crops[p.Crop].FinalStage;

        /// <summary>Blok 2x2 tanaman matang sejenis yang memuat (x, y), atau null.</summary>
        public List<(int, int)>? FindBlock(int x, int y)
        {
            string? crop = Plots.TryGetValue((x, y), out var p) ? p.Crop : null;
            foreach (int ox in new[] { 0, -1 })
                foreach (int oy in new[] { 0, -1 })
                {
                    var tiles = new List<(int, int)>();
                    for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) tiles.Add((x + ox + i, y + oy + j));
                    if (tiles.All(t => IsRipe(t.Item1, t.Item2) && Plots[t].Crop == crop)) return tiles;
                }
            return null;
        }

        public int PlotsWithCrops => Plots.Values.Count(p => p.Crop != null);

        public ActionResult Till(int x, int y)
        {
            if (!IsFarmable(x, y)) return new(ActionKind.Till, false, "Hanya bisa mencangkul di dalam kebun.");
            if (Plots.ContainsKey((x, y))) return new(ActionKind.Till, false, "Tanah ini sudah digemburkan.");
            Plots[(x, y)] = new Plot();
            return new(ActionKind.Till, true);
        }

        public ActionResult Water(int x, int y)
        {
            if (!Plots.TryGetValue((x, y), out var plot)) return new(ActionKind.Water, false, "Cangkul tanahnya dulu.");
            if (plot.Watered) return new(ActionKind.Water, false, "Tanah ini sudah basah.");
            plot.Watered = true;
            return new(ActionKind.Water, true);
        }

        public ActionResult Plant(int x, int y, string cropId, bool hasSeed)
        {
            if (!Plots.TryGetValue((x, y), out var plot)) return new(ActionKind.Plant, false, "Cangkul tanahnya dulu.");
            if (plot.Crop != null) return new(ActionKind.Plant, false, "Sudah ada tanaman di sini.");
            if (!hasSeed) return new(ActionKind.Plant, false, "Benihnya habis. Beli di Lapak Darto.");
            plot.Crop = cropId; plot.Stage = 0; plot.Progress = 0;
            return new(ActionKind.Plant, true, Item: Crops[cropId].Seed);
        }

        public ActionResult Harvest(int x, int y)
        {
            if (!IsRipe(x, y)) return new(ActionKind.Harvest, false);
            var plot = Plots[(x, y)];
            var def = Crops[plot.Crop!];
            if (def.RegrowDays is int regrow)
            {
                plot.Stage = def.FinalStage - 1;
                plot.Progress = def.StageDays[^1] - regrow;
            }
            else
            {
                plot.Crop = null; plot.Stage = 0; plot.Progress = 0;
            }
            return new(ActionKind.Harvest, true, Item: def.Produce);
        }

        /// <summary>Tumbuhkan tanaman yang disiram, keringkan semua petak. Mengembalikan jumlah yang naik tahap.</summary>
        public int EndDay()
        {
            int advanced = 0;
            foreach (var plot in Plots.Values)
            {
                if (plot.Crop != null && plot.Watered)
                {
                    var def = Crops[plot.Crop];
                    if (plot.Stage < def.FinalStage)
                    {
                        plot.Progress++;
                        if (plot.Progress >= def.StageDays[plot.Stage]) { plot.Stage++; plot.Progress = 0; advanced++; }
                    }
                }
                plot.Watered = false;
            }
            return advanced;
        }

        public void WaterAll()
        {
            foreach (var p in Plots.Values) p.Watered = true;
        }

        public JsonArray ToJson()
        {
            var a = new JsonArray();
            foreach (var ((x, y), p) in Plots) a.Add(new JsonArray(x, y, p.Watered, p.Crop, p.Stage, p.Progress));
            return a;
        }

        public void LoadJson(JsonNode? data)
        {
            Plots.Clear();
            foreach (var e in data?.AsArray() ?? new JsonArray())
                Plots[(J.I(e![0]), J.I(e[1]))] = new Plot { Watered = (bool)e[2]!, Crop = (string?)e[3], Stage = J.I(e[4]), Progress = J.I(e[5]) };
        }
    }

    /// <summary>
    /// ABSTRAKSI + POLIMORFISME: alat di hotbar. Template method Use(): alat apa pun memanen
    /// tanaman matang dulu, baru menjalankan Apply() milik subclass. Preview() mewarnai kursor.
    /// </summary>
    public abstract class FarmTool
    {
        public abstract string Id { get; }            // kunci biaya energi: hoe | can | seed
        public abstract string Icon { get; }
        public abstract string Label { get; }
        public abstract string Desc { get; }
        public virtual string? Item => null;          // barang yang jumlahnya ditampilkan (benih)
        public virtual bool Swings => true;           // diayunkan di tangan pemain saat dipakai

        protected abstract ActionResult Apply(FarmField field, Inventory inv, int x, int y);
        protected abstract bool WouldWork(FarmField field, Inventory inv, Plot? plot, int x, int y);

        public ActionResult Use(FarmField field, Inventory inv, int x, int y)
        {
            if (field.IsRipe(x, y))
            {
                var res = field.Harvest(x, y);
                inv.Add(res.Item!, 1);
                return res;
            }
            return Apply(field, inv, x, y);
        }

        public Preview PreviewAt(FarmField field, Inventory inv, int x, int y)
        {
            if (!field.Map.InBounds(x, y) || !field.IsFarmable(x, y)) return Preview.Info;
            if (field.IsRipe(x, y)) return Preview.Ok;
            field.Plots.TryGetValue((x, y), out var plot);
            return WouldWork(field, inv, plot, x, y) ? Preview.Ok : Preview.Bad;
        }
    }

    public sealed class HoeTool : FarmTool
    {
        public override string Id => "hoe";
        public override string Icon => "icon_hoe";
        public override string Label => "Cangkul";
        public override string Desc => $"Gemburkan tanah di dalam kebun.\nEnergi -{Balance.EnergyCost["hoe"]}";
        protected override ActionResult Apply(FarmField f, Inventory inv, int x, int y) => f.Till(x, y);
        protected override bool WouldWork(FarmField f, Inventory inv, Plot? plot, int x, int y) => plot == null;
    }

    public sealed class CanTool : FarmTool
    {
        public override string Id => "can";
        public override string Icon => "icon_can";
        public override string Label => "Penyiram";
        public override string Desc => $"Siram tanah agar tanaman tumbuh malam ini.\nEnergi -{Balance.EnergyCost["can"]}";
        protected override ActionResult Apply(FarmField f, Inventory inv, int x, int y) => f.Water(x, y);
        protected override bool WouldWork(FarmField f, Inventory inv, Plot? plot, int x, int y) => plot is { Watered: false };
    }

    public sealed class SeedTool : FarmTool
    {
        private readonly CropDef crop;
        public SeedTool(CropDef crop) => this.crop = crop;

        public string CropId => crop.Id;
        public override string Id => "seed";
        public override string Icon => "icon_" + crop.Seed;
        public override string Label => "Benih " + crop.Name;
        public override string? Item => crop.Seed;
        public override bool Swings => false;

        public override string Desc
        {
            get
            {
                string extra = crop.RegrowDays is int r ? $"\nPanen ulang tiap {r} hari." : "";
                return $"Tanam di tanah yang sudah dicangkul.\nSiap panen setelah {crop.StageDays.Sum()} malam disiram.{extra}\nEnergi -{Balance.EnergyCost["seed"]}";
            }
        }

        protected override ActionResult Apply(FarmField f, Inventory inv, int x, int y)
        {
            var res = f.Plant(x, y, crop.Id, inv.Count(crop.Seed) > 0);
            if (res.Ok) inv.Remove(crop.Seed, 1);
            return res;
        }

        protected override bool WouldWork(FarmField f, Inventory inv, Plot? plot, int x, int y)
            => plot is { Crop: null } && inv.Count(crop.Seed) > 0;
    }

    /// <summary>Angka keseimbangan permainan (config.py).</summary>
    public static class Balance
    {
        public const float WalkSpeed = 56f, RunMult = 1.7f, CameraSmoothing = 7f;
        public const float InteractRadius = 1.9f, ToolReach = 2.8f;
        public const int EnergyMax = 100, FaintEnergy = 60, MasteryStreak = 3;
        public const float DayRealSeconds = 240f;
        public static readonly Dictionary<string, int> EnergyCost = new() { ["hoe"] = 3, ["can"] = 2, ["seed"] = 1 };
    }
}
