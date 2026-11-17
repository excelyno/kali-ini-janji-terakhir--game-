using System.Text.Json.Nodes;

namespace Tandur.Core
{
    /// <summary>ABSTRAKSI: definisi barang. Benih dan hasil panen berbeda perilaku harga (POLIMORFISME).</summary>
    public abstract class ItemDef
    {
        protected ItemDef(string id, string name, string desc)
        {
            Id = id; Name = name; Desc = desc;
        }

        public string Id { get; }
        public string Name { get; }
        public string Desc { get; }
        public string Icon => "icon_" + Id;
        public abstract string Kind { get; }
        public virtual int Buy => 0;
        public virtual int Sell => 0;
        public virtual bool Sellable => false;
    }

    public sealed class SeedDef : ItemDef
    {
        private readonly int buy;
        public SeedDef(string id, string name, string desc, int buy) : base(id, name, desc) => this.buy = buy;
        public override string Kind => "seed";
        public override int Buy => buy;
    }

    public sealed class ProduceDef : ItemDef
    {
        private readonly int sell;
        public ProduceDef(string id, string name, string desc, int sell) : base(id, name, desc) => this.sell = sell;
        public override string Kind => "produce";
        public override int Sell => sell;
        public override bool Sellable => true;
    }

    public sealed class ItemDb
    {
        private readonly Dictionary<string, ItemDef> items = new();

        public ItemDb(string path)
        {
            foreach (var (id, n) in JsonNode.Parse(File.ReadAllText(path))!.AsObject())
            {
                string name = (string)n!["name"]!, desc = (string?)n["desc"] ?? "";
                items[id] = (string)n["kind"]! == "seed"
                    ? new SeedDef(id, name, desc, J.In(n["buy"]) ?? 0)
                    : new ProduceDef(id, name, desc, J.In(n["sell"]) ?? 0);
            }
        }

        public ItemDef this[string id] => items[id];
        public bool Has(string id) => items.ContainsKey(id);
        public string Name(string id) => items.TryGetValue(id, out var d) ? d.Name : id;
        public IEnumerable<ItemDef> All => items.Values;
        public IEnumerable<ItemDef> Produce => items.Values.Where(i => i.Sellable);
    }

    /// <summary>Jumlah barang di atas GameState. Hasil panen dicatat per kualitas (1-3 bintang).
    /// ENKAPSULASI: kunci penyimpanan disembunyikan; luar hanya memakai Add/Remove/Count.</summary>
    public sealed class Inventory
    {
        public static readonly Dictionary<int, double> StarMult = new() { [1] = 1.0, [2] = 1.25, [3] = 1.5 };
        private const string Prefix = "item_";
        private readonly GameState state;

        public Inventory(GameState state, ItemDb db)
        {
            this.state = state;
            Db = db;
        }

        public ItemDb Db { get; }

        private static string Key(string id, int stars = 1) => Prefix + id + (stars == 1 ? "" : "#" + stars);

        public int Count(string id) => Enumerable.Range(1, 3).Sum(s => state.GetInt(Key(id, s)));
        public Dictionary<int, int> Breakdown(string id) => Enumerable.Range(1, 3).ToDictionary(s => s, s => state.GetInt(Key(id, s)));
        public int Add(string id, int amount = 1, int stars = 1) => state.Add(Key(id, stars), amount);

        public bool Remove(string id, int amount = 1)
        {
            if (Count(id) < amount) return false;
            for (int s = 1; s <= 3 && amount > 0; s++)        // kualitas terendah dulu
            {
                int take = Math.Min(state.GetInt(Key(id, s)), amount);
                if (take > 0) { state.Add(Key(id, s), -take); amount -= take; }
            }
            return true;
        }

        public int Gold => state.GetInt("gold");

        public int SellAllProduce()
        {
            int total = 0;
            foreach (var item in Db.Produce)
                foreach (var (s, n) in Breakdown(item.Id))
                    if (n > 0)
                    {
                        total += n * (int)Math.Round(item.Sell * StarMult[s], MidpointRounding.ToEven);
                        state.Set(Key(item.Id, s), 0);
                    }
            state.Add("gold", total);
            return total;
        }
    }

    public sealed record DebtReport(bool Grace, int Interest, int Fee, bool Missed, int Shortfall, int Remaining, double Rate, int Minimum);

    /// <summary>Utang warisan: pelajaran literasi keuangan musim demi musim, aturan polos di atas GameState.</summary>
    public sealed class Debt
    {
        public const int StartDebt = 6000, Installment = 300;
        public const double BaseRate = 0.05, RateStep = 0.01, MaxRate = 0.09, LateFee = 0.10;
        private readonly GameState s;

        public Debt(GameState state)
        {
            s = state;
            s.SetDefault("debt", StartDebt);
            s.SetDefault("debt_start", StartDebt);
            s.SetDefault("debt_paid", 0);
            s.SetDefault("debt_season_paid", 0);
            s.SetDefault("debt_missed", 0);
            s.SetDefault("debt_rate", BaseRate);
            s.SetDefault("debt_grace", 1);
        }

        public int Remaining => s.GetInt("debt");
        public bool Free => Remaining <= 0;
        public double Rate => s.GetDouble("debt_rate", BaseRate);
        public int SeasonPaid => s.GetInt("debt_season_paid");
        public int Minimum => Math.Min(Installment, Remaining);
        public int SeasonLeft => Math.Max(0, Minimum - SeasonPaid);
        public double PaidFraction => Math.Min(1.0, s.GetInt("debt_paid") / (double)Math.Max(1, s.GetInt("debt_start", StartDebt)));
        public static int DaysUntilVisit(int day) => GameClock.DaysPerSeason - (day - 1) % GameClock.DaysPerSeason;

        public int Pay(int amount)
        {
            int gold = s.GetInt("gold");
            int paid = Math.Max(0, Math.Min(amount, Math.Min(gold, Remaining)));
            if (paid > 0)
            {
                s.Set("gold", gold - paid);
                s.Set("debt", Remaining - paid);
                s.Add("debt_paid", paid);
                s.Add("debt_season_paid", paid);
                if (Remaining <= 0) s.Set("debt_free", true);
            }
            return paid;
        }

        /// <summary>Dipanggil sekali saat musim berakhir. null bila sudah lunas.</summary>
        public DebtReport? NewSeason()
        {
            if (Free) return null;
            int before = Remaining;
            if (s.GetInt("debt_grace") > 0)
            {
                s.Set("debt_grace", s.GetInt("debt_grace") - 1);
                s.Set("debt_season_paid", 0);
                return new DebtReport(true, 0, 0, false, 0, Remaining, Rate, Minimum);
            }
            int owedMin = Math.Min(Installment, before);
            int shortfall = Math.Max(0, owedMin - SeasonPaid);
            int interest = (int)Math.Round(before * Rate, MidpointRounding.ToEven);
            s.Set("debt", before + interest);
            int fee = 0;
            if (shortfall > 0)
            {
                fee = (int)Math.Round(shortfall * LateFee, MidpointRounding.ToEven);
                s.Add("debt", fee);
                s.Add("debt_missed", 1);
                s.Set("debt_rate", Math.Min(MaxRate, Math.Round(Rate + RateStep, 4)));
            }
            else
            {
                s.Set("debt_missed", 0);
                s.Set("debt_rate", BaseRate);
            }
            s.Set("debt_season_paid", 0);
            return new DebtReport(false, interest, fee, shortfall > 0, shortfall, Remaining, Rate, Minimum);
        }
    }

    /// <summary>"Catatan Tani": fakta bertani yang terbuka lewat minigame.</summary>
    public sealed record Fact(string Id, string Title, string Text, string? Crop);

    public sealed class Journal
    {
        private const string Key = "facts";
        private readonly GameState state;

        public Journal(string path, GameState state)
        {
            this.state = state;
            foreach (var (k, v) in JsonNode.Parse(File.ReadAllText(path))!.AsObject())
                Facts[k] = new Fact(k, (string)v!["title"]!, (string)v["text"]!, (string?)v["crop"]);
        }

        public Dictionary<string, Fact> Facts { get; } = new();

        public List<string> UnlockedIds() => (state.Get(Key) as JsonArray)?.Select(n => (string)n!).ToList() ?? new();
        public bool IsUnlocked(string id) => UnlockedIds().Contains(id);

        public List<Fact> Unlock(IEnumerable<string> ids)
        {
            var have = UnlockedIds();
            var fresh = new List<Fact>();
            foreach (var id in ids)
                if (Facts.ContainsKey(id) && !have.Contains(id)) { have.Add(id); fresh.Add(Facts[id]); }
            state.Set(Key, new JsonArray(have.Select(h => (JsonNode?)h).ToArray()));
            return fresh;
        }
    }

    public static class Fmt
    {
        /// <summary>6000 -> "6.000" (pemisah ribuan gaya Indonesia).</summary>
        public static string N(long n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
