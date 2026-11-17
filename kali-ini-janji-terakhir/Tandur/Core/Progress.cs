using System.Text.Json.Nodes;

namespace Tandur.Core
{
    /// <summary>Apa yang baru terjadi di dunia, dilaporkan ke log misi.</summary>
    public sealed record QuestEvent(string Type, int Amount = 1, string? Kind = null, string? Crop = null, string? Biome = null, int Stars = 3);

    /// <summary>
    /// ABSTRAKSI: satu tujuan misi. Tiap jenis tujuan (aksi, panen, jual, jelajah, ...) cocok dengan
    /// event berbeda (POLIMORFISME). PaidTotalObjective bahkan tidak menghitung event sama sekali,
    /// melainkan membaca total utang yang sudah dibayar.
    /// </summary>
    public abstract class Objective
    {
        protected Objective(string type, int n, string label)
        {
            Type = type; N = n; Label = label;
        }

        public string Type { get; }
        public int N { get; }
        public string Label { get; }

        public virtual bool Matches(QuestEvent e) => e.Type == Type;
        public virtual int Current(int counted, GameState state) => counted;

        public static Objective From(JsonNode o)
        {
            string type = (string)o["type"]!, label = (string?)o["label"] ?? "";
            int n = J.I(o["n"]);
            return type switch
            {
                "action" => new ActionObjective(n, (string?)o["kind"], label),
                "harvest" => new HarvestObjective(n, (string?)o["crop"], J.In(o["min_stars"]) ?? 0, label),
                "discover" => new DiscoverObjective(n, (string?)o["biome"], label),
                "paid_total" => new PaidTotalObjective(n, label),
                _ => new CounterObjective(type, n, label),       // earn, memory, sit
            };
        }
    }

    public sealed class CounterObjective : Objective
    {
        public CounterObjective(string type, int n, string label) : base(type, n, label) { }
    }

    public sealed class ActionObjective : Objective
    {
        private readonly string? kind;
        public ActionObjective(int n, string? kind, string label) : base("action", n, label) => this.kind = kind;
        public override bool Matches(QuestEvent e) => base.Matches(e) && (kind == null || kind == e.Kind);
    }

    public sealed class HarvestObjective : Objective
    {
        private readonly string? crop;
        private readonly int minStars;

        public HarvestObjective(int n, string? crop, int minStars, string label) : base("harvest", n, label)
        {
            this.crop = crop;
            this.minStars = minStars;
        }

        public override bool Matches(QuestEvent e) => base.Matches(e) && (crop == null || crop == e.Crop) && e.Stars >= minStars;
    }

    public sealed class DiscoverObjective : Objective
    {
        private readonly string? biome;
        public DiscoverObjective(int n, string? biome, string label) : base("discover", n, label) => this.biome = biome;
        public override bool Matches(QuestEvent e) => base.Matches(e) && (biome == null || biome == e.Biome);
    }

    public sealed class PaidTotalObjective : Objective
    {
        public PaidTotalObjective(int n, string label) : base("paid_total", n, label) { }
        public override bool Matches(QuestEvent e) => false;
        public override int Current(int counted, GameState state) => state.GetInt("debt_paid");
    }

    public sealed record QuestReward(int Gold, Dictionary<string, int> Items, int Rings);

    public sealed class QuestDef
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Desc { get; init; }
        public required string Giver { get; init; }
        public int Chapter { get; init; }
        public required string[] Requires { get; init; }
        public required Objective[] Objectives { get; init; }
        public required QuestReward Reward { get; init; }
        public string Lesson { get; init; } = "";
        public int MinAct { get; init; }
    }

    /// <summary>Misi berbasis data (quests.json). Progres disimpan di GameState["quests"].</summary>
    public sealed class QuestLog
    {
        private const string Key = "quests";
        private readonly GameState state;

        public QuestLog(string path, GameState state)
        {
            this.state = state;
            foreach (var q in JsonNode.Parse(File.ReadAllText(path))!.AsArray())
            {
                var r = q!["reward"] as JsonObject ?? new JsonObject();
                var items = (r["items"] as JsonObject)?.ToDictionary(kv => kv.Key, kv => J.I(kv.Value)) ?? new();
                Defs[(string)q["id"]!] = new QuestDef
                {
                    Id = (string)q["id"]!, Title = (string)q["title"]!, Desc = (string)q["desc"]!,
                    Giver = (string?)q["giver"] ?? "Papan Desa", Chapter = J.In(q["chapter"]) ?? 1,
                    Requires = (q["requires"] as JsonArray)?.Select(x => (string)x!).ToArray() ?? Array.Empty<string>(),
                    Objectives = q["objectives"]!.AsArray().Select(o => Objective.From(o!)).ToArray(),
                    Reward = new QuestReward(J.In(r["gold"]) ?? 0, items, J.In(r["rings"]) ?? 0),
                    Lesson = (string?)q["lesson"] ?? "", MinAct = J.In(q["min_act"]) ?? 0,
                };
            }
            foreach (var q in Defs.Values)
                foreach (var req in q.Requires)
                    if (!Defs.ContainsKey(req)) throw new InvalidDataException($"misi {q.Id} butuh misi tak dikenal {req}");
            state.SetDefault(Key, new JsonObject());
            Refresh();
        }

        public Dictionary<string, QuestDef> Defs { get; } = new();
        private JsonObject Entries => (JsonObject)state.Get(Key)!;

        public string Status(string id) => Entries[id] is JsonObject e ? (string)e["status"]! : "locked";

        public List<QuestDef> Refresh()
        {
            var started = new List<QuestDef>();
            int act = state.GetInt("act");
            foreach (var q in Defs.Values)
                if (!Entries.ContainsKey(q.Id) && act >= q.MinAct && q.Requires.All(r => Status(r) == "done"))
                {
                    var p = new JsonArray();
                    foreach (var _ in q.Objectives) p.Add(0);
                    Entries[q.Id] = new JsonObject { ["status"] = "active", ["p"] = p };
                    started.Add(q);
                }
            return started;
        }

        public List<QuestDef> Active() => Defs.Values.Where(q => Status(q.Id) == "active").ToList();
        public List<QuestDef> Done() => Defs.Values.Where(q => Status(q.Id) == "done").ToList();

        private int Counted(QuestDef q, int i) => J.I(Entries[q.Id]!["p"]![i]);

        public (int Cur, int N) Progress(QuestDef q, int i)
        {
            var o = q.Objectives[i];
            return (Math.Min(o.Current(Counted(q, i), state), o.N), o.N);
        }

        public float Fraction(QuestDef q)
            => q.Objectives.Select((o, i) => Math.Min(1f, Progress(q, i).Cur / (float)Math.Max(1, o.N))).Average();

        /// <summary>Laporkan sesuatu yang terjadi. Mengembalikan (misi selesai, misi baru dimulai).</summary>
        public (List<QuestDef> Completed, List<QuestDef> Started) Notify(QuestEvent e)
        {
            var completed = new List<QuestDef>();
            foreach (var q in Active())
            {
                var p = Entries[q.Id]!["p"]!.AsArray();
                for (int i = 0; i < q.Objectives.Length; i++)
                    if (q.Objectives[i].Matches(e)) p[i] = Math.Min(q.Objectives[i].N, J.I(p[i]) + e.Amount);
                if (q.Objectives.Select((o, i) => Progress(q, i).Cur >= o.N).All(x => x))
                {
                    Entries[q.Id]!["status"] = "done";
                    completed.Add(q);
                }
            }
            return (completed, Refresh());
        }

        public (List<QuestDef>, List<QuestDef>) Recheck() => Notify(new QuestEvent("noop"));
    }

    /// <summary>Busur cerita: enam babak dari kedatangan sampai festival.</summary>
    public sealed class Story
    {
        public const int Prolog = 0, Bertahan = 1, Akar = 2, Kemarau = 3, Lentera = 4, Epilog = 5;
        public static readonly (string Head, string Name)[] Acts =
        {
            ("Prolog", "Kedatangan"), ("Babak 1", "Bertahan"), ("Babak 2", "Akar"),
            ("Babak 3", "Kemarau"), ("Babak 4", "Lentera"), ("Epilog", "Festival Panen"),
        };
        public const int DroughtDays = GameClock.DaysPerSeason, DroughtPaid = 1500, DroughtLatest = 3 * GameClock.DaysPerSeason;
        public const double DroughtPriceBonus = 0.25;
        public const int DroughtWaterCost = 1;
        public static readonly string[] Memories = { "scarecrow", "well", "windmill", "tree", "lantern" };

        private static readonly Dictionary<int, string[]> Thoughts = new()
        {
            [Prolog] = new[] { "Sunyi. Tapi bukan sunyi yang kosong. Lebih seperti tanah yang sedang menahan napas.",
                               "Bibi Sekar pernah duduk di sini juga, barangkali. Memandang hal yang sama." },
            [Bertahan] = new[] { "Enam ribu keping. Angka itu terasa lebih kecil kalau dipecah jadi musim demi musim.",
                                 "Tangan pegal, tapi tunas pertama itu milikku sendiri." },
            [Akar] = new[] { "Dulu aku cuma numpang lewat. Sekarang orang di sini tahu namaku.",
                             "Akar tidak terlihat, tapi dialah yang membuat pohon tetap berdiri saat angin datang." },
            [Kemarau] = new[] { "Langit biru tanpa awan. Dulu itu kabar baik.",
                                "Tabungan bukan untuk hari cerah. Tabungan itu untuk hari seperti ini.",
                                "Sungai surut, tapi belum habis. Aku juga belum." },
            [Lentera] = new[] { "Hujan pertama setelah kemarau baunya lain. Seperti tanah yang memaafkan.",
                                "Lentera-lentera itu bukan untuk menerangi jalan pergi. Mereka menandai jalan pulang." },
            [Epilog] = new[] { "Utang sudah lunas. Yang tersisa hanya ladang, musim, dan pilihan-pilihanku sendiri.",
                               "Bebas itu ternyata tenang. Bukan sorak-sorai, tapi napas yang panjang." },
        };
        private static readonly string[] NightThoughts =
        {
            "Bintang di sini lebih banyak daripada di kota. Atau mungkin aku baru sempat melihatnya.",
            "Jendela-jendela menyala satu per satu. Setiap cahaya itu satu keluarga yang bertahan.",
            "Besok tanah itu masih ada. Itu sudah cukup alasan untuk tidur nyenyak.",
        };

        private readonly GameState s;

        public Story(GameState state)
        {
            s = state;
            s.SetDefault("act", Prolog);
            s.SetDefault("act_day", 1);
            s.SetDefault("memories", 0);
            s.SetDefault("debt_missed_total", 0);
        }

        public int Act => s.GetInt("act");
        public string Title => $"{Acts[Act].Head}: {Acts[Act].Name}";
        public bool Drought => Act == Kemarau;
        public int DroughtDaysLeft(int day) => Drought ? Math.Max(0, s.GetInt("drought_until") - day) : 0;

        private int SetAct(int act, int day)
        {
            s.Set("act", act);
            s.Set("act_day", day);
            if (act == Kemarau) s.Set("drought_until", day + DroughtDays);
            if (act == Epilog) s.Set("ending", Ending(day));
            return act;
        }

        public string Ending(int day)
        {
            if (s.GetInt("memories") >= Memories.Length) return "penjaga";
            if (s.GetInt("debt_missed_total") >= 2) return "gigih";
            if (day <= 6 * GameClock.DaysPerSeason) return "cepat";
            return "tekun";
        }

        public int? Advance(int day, Func<string, bool> questDone, bool newDay = false)
        {
            int act = Act;
            if (act >= Epilog) return null;
            if (s.GetBool("debt_free")) return SetAct(Epilog, day);
            if (act == Prolog && (day >= 2 || questDone("q_till"))) return SetAct(Bertahan, day);
            if (act == Bertahan && questDone("q_first_pay")) return SetAct(Akar, day);
            if (act == Akar && newDay && (day - 1) % GameClock.DaysPerSeason == 0)
            {
                int waited = day - s.GetInt("act_day", day);
                if (s.GetInt("debt_paid") >= DroughtPaid || waited >= DroughtLatest) return SetAct(Kemarau, day);
            }
            if (act == Kemarau && newDay && day >= s.GetInt("drought_until", day)) return SetAct(Lentera, day);
            return null;
        }

        public string Thought(float minutes, Random rng)
        {
            var pool = minutes >= 19 * 60 ? NightThoughts : Thoughts[Act];
            return pool[rng.Next(pool.Length)];
        }

        public bool Remember(string name)
        {
            string key = $"mem_{name}";
            if (!Memories.Contains(name) || s.GetBool($"{key}_counted")) return false;
            s.Set($"{key}_counted", true);
            s.Add("memories", 1);
            return true;
        }
    }

    /// <summary>Alur utama sebagai rantai "beat" (story.json): selalu tepat satu hal yang diinginkan cerita darimu.</summary>
    public sealed class Storyline
    {
        public static readonly (string Name, (int X, int Y) Tile)[] MemorySpots = { ("well", (12, 10)), ("windmill", (36, 26)), ("scarecrow", (26, 27)) };
        private readonly GameState s;

        public Storyline(string path, GameState state)
        {
            Beats = JsonNode.Parse(File.ReadAllText(path))!.AsArray().Select(b => b!.AsObject()).ToList();
            if (Beats.Select(b => (string)b["id"]!).Distinct().Count() != Beats.Count) throw new InvalidDataException("story.json: id beat ganda");
            s = state;
            s.SetDefault("beat", 0);
            s.SetDefault("beat_on", false);
        }

        public List<JsonObject> Beats { get; }
        public int Index => s.GetInt("beat");
        public JsonObject? Pending => Index < Beats.Count ? Beats[Index] : null;
        public JsonObject? Current => s.GetBool("beat_on") ? Pending : null;

        public JsonObject? Begin(bool newDay = false)
        {
            var b = Pending;
            if (b == null || s.GetBool("beat_on") || s.GetInt("act") < (J.In(b["act"]) ?? 0)) return null;
            if (((bool?)b["morning"] ?? false) && !newDay) return null;
            s.Set("beat_on", true);
            return b;
        }

        public bool IsDone(JsonObject beat, Func<string, bool> questDone)
        {
            if (beat["done"] is not JsonObject d) return false;
            if (d["quest"] is { } q) return questDone((string)q!);
            if (d["flag"] is { } f) return s.GetBool((string)f!);
            if (d["var"] is { } v) return s.GetInt((string)v!) >= (J.In(d["min"]) ?? 1);
            if (d["act"] is { } a) return s.GetInt("act") >= J.I(a);
            if ((bool?)d["talk"] ?? false) return s.GetBool($"talked_{beat["id"]}");
            return false;
        }

        public JsonObject? FinishIfDone(Func<string, bool> questDone)
        {
            var b = Current;
            if (b == null || !IsDone(b, questDone)) return null;
            s.Set("beat", Index + 1);
            s.Set("beat_on", false);
            return b;
        }

        public string? TalkScript(string npcId)
        {
            var b = Current;
            if (b != null && b["talk"] is { } t && (string?)b["target"]?["npc"] == npcId && !s.GetBool($"talked_{b["id"]}"))
                return (string)t!;
            return null;
        }

        public void Talked(string beatId) => s.Set($"talked_{beatId}", true);

        /// <summary>("npc", id) | ("tile", x, y) | ("script", name, x, y) | null.</summary>
        public (string Kind, string Arg, int X, int Y)? Target()
        {
            if (Current?["target"] is not JsonObject t) return null;
            if (t["npc"] is { } n) return ("npc", (string)n!, 0, 0);
            if ((bool?)t["memory"] ?? false)
            {
                foreach (var (name, tile) in MemorySpots)
                    if (!s.GetBool($"mem_{name}")) return ("tile", "", tile.X, tile.Y);
                return null;
            }
            var tl = t["tile"] as JsonArray;
            int x = tl != null ? J.I(tl[0]) : 0, y = tl != null ? J.I(tl[1]) : 0;
            if (t["script"] is { } sc) return ("script", (string)sc!, x, y);
            return ("tile", "", x, y);
        }
    }

    /// <summary>Simpan / muat: satu file JSON, ditulis atomik dengan cadangan bergulir.</summary>
    public sealed class SaveManager
    {
        public const int Version = 1;
        private static readonly string[] Required = { "version", "state", "clock", "farm", "world", "player" };

        public SaveManager(string? path = null)
        {
            string dir = Environment.GetEnvironmentVariable("TANDUR_SAVE_DIR")
                         ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TANDUR_CS");
            SavePath = path ?? Path.Combine(dir, "slot1.json");
        }

        public string SavePath { get; }
        private string Backup => Path.ChangeExtension(SavePath, ".bak");

        public void Save(JsonObject data)
        {
            data["version"] = Version;
            data["saved_at"] = DateTime.Now.ToString("s");
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            string tmp = Path.ChangeExtension(SavePath, ".tmp");
            File.WriteAllText(tmp, data.ToJsonString());
            if (File.Exists(SavePath)) File.Copy(SavePath, Backup, true);
            File.Move(tmp, SavePath, true);
        }

        private static JsonObject? Read(string path)
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject d) return null;
                if (Required.Any(k => !d.ContainsKey(k))) return null;
                if (J.In(d["version"]) is not int v || v > Version) return null;
                return d;
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
            {
                return null;
            }
        }

        public JsonObject? Load()
        {
            foreach (var p in new[] { SavePath, Backup })
                if (File.Exists(p) && Read(p) is { } d) return d;
            return null;
        }

        public (int Day, string Season, int Gold, int Debt)? Info()
        {
            var d = Load();
            if (d == null) return null;
            int day = J.In(d["clock"]?["day"]) ?? 1;
            var st = new GameState(d["state"] as JsonObject);
            return (day, GameClock.Seasons[(day - 1) / GameClock.DaysPerSeason % 4], st.GetInt("gold"), st.GetInt("debt"));
        }

        public void Delete()
        {
            foreach (var p in new[] { SavePath, Backup, Path.ChangeExtension(SavePath, ".tmp") })
                try { File.Delete(p); } catch (IOException) { }
        }
    }
}
