using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tandur.Core
{
    /// <summary>
    /// Flag cerita dan penghitung yang dipakai bersama gameplay dan sistem VN. Data polos saja
    /// (JSON), jadi simpan/muat cukup ToJson()/FromJson(). ENKAPSULASI: akses hanya lewat metode bertipe.
    /// </summary>
    public sealed class GameState
    {
        private readonly JsonObject vars;

        public GameState(JsonObject? initial = null)
        {
            vars = new JsonObject { ["player_name"] = "Nak" };
            if (initial != null)
                foreach (var (k, v) in initial) vars[k] = v?.DeepClone();
        }

        public JsonObject Vars => vars;

        public bool Has(string name) => vars.ContainsKey(name);
        public JsonNode? Get(string name) => vars[name];

        public void SetDefault(string name, JsonNode? value)
        {
            if (!vars.ContainsKey(name)) vars[name] = value;
        }

        public void Set(string name, JsonNode? value) => vars[name] = value;

        public int GetInt(string name, int def = 0)
        {
            var n = vars[name];
            if (n is not JsonValue v) return def;
            if (v.TryGetValue(out int i)) return i;
            if (v.TryGetValue(out long l)) return (int)l;
            if (v.TryGetValue(out double d)) return (int)d;
            if (v.TryGetValue(out bool b)) return b ? 1 : 0;
            if (v.TryGetValue(out string? s) && int.TryParse(s, out i)) return i;
            return def;
        }

        public double GetDouble(string name, double def = 0)
        {
            var n = vars[name];
            if (n is not JsonValue v) return def;
            if (v.TryGetValue(out double d)) return d;
            if (v.TryGetValue(out int i)) return i;
            if (v.TryGetValue(out long l)) return l;
            if (v.TryGetValue(out bool b)) return b ? 1 : 0;
            return def;
        }

        public string GetString(string name, string def = "")
        {
            var n = vars[name];
            if (n == null) return def;
            if (n is JsonValue v && v.TryGetValue(out string? s)) return s;
            if (n is JsonValue b && b.TryGetValue(out bool bb)) return bb ? "True" : "False";
            if (n is JsonValue dv && dv.TryGetValue(out double d) && d == Math.Floor(d)) return ((long)d).ToString();
            return n.ToJsonString();
        }

        public bool GetBool(string name) => Truthy(vars[name]);

        public int Add(string name, int amount)
        {
            int v = GetInt(name) + amount;
            vars[name] = v;
            return v;
        }

        /// <summary>Kebenaran gaya Python: 0, "", false, null, list kosong = salah.</summary>
        public static bool Truthy(JsonNode? n)
        {
            switch (n)
            {
                case null: return false;
                case JsonArray a: return a.Count > 0;
                case JsonObject o: return o.Count > 0;
                case JsonValue v:
                    if (v.TryGetValue(out bool b)) return b;
                    if (v.TryGetValue(out double d)) return d != 0;
                    if (v.TryGetValue(out int i)) return i != 0;
                    if (v.TryGetValue(out long l)) return l != 0;
                    if (v.TryGetValue(out string? s)) return s.Length > 0;
                    if (v.GetValueKind() == JsonValueKind.Number) return v.ToJsonString() != "0";
                    return true;
            }
            return true;
        }

        public JsonObject ToJson() => (JsonObject)vars.DeepClone();
        public static GameState FromJson(JsonObject data) => new(data);
    }

    /// <summary>Pub/sub kecil: skrip VN memancarkan event, sistem gameplay berlangganan.</summary>
    public sealed class EventBus
    {
        private readonly Dictionary<string, List<Action<string[]>>> handlers = new();

        public void On(string name, Action<string[]> h)
        {
            if (!handlers.TryGetValue(name, out var l)) handlers[name] = l = new();
            l.Add(h);
        }

        public void Off(string name, Action<string[]> h)
        {
            if (handlers.TryGetValue(name, out var l)) l.Remove(h);
        }

        public void Emit(string name, params string[] args)
        {
            if (handlers.TryGetValue(name, out var l))
                foreach (var h in l.ToList()) h(args);
        }
    }

    /// <summary>Waktu dalam game: menit, hari, musim, dan tint langit untuk saat itu.</summary>
    public sealed class GameClock
    {
        public const int DayStart = 6 * 60, DayEnd = 24 * 60, DaysPerSeason = 10;
        public static readonly string[] Seasons = { "Semi", "Panas", "Gugur", "Dingin" };

        private static readonly (int M, float R, float G, float B)[] Tint =
        {
            (6 * 60, 0.93f, 0.82f, 0.80f), (8 * 60, 1.00f, 0.97f, 0.90f), (12 * 60, 1f, 1f, 1f), (17 * 60, 1.00f, 0.96f, 0.86f),
            (18 * 60 + 30, 1.00f, 0.76f, 0.58f), (20 * 60, 0.70f, 0.60f, 0.80f), (21 * 60 + 30, 0.48f, 0.52f, 0.78f), (24 * 60, 0.40f, 0.45f, 0.74f),
        };

        private readonly float rate;

        public GameClock(float realSecondsPerDay = 240f) => rate = (DayEnd - DayStart) / realSecondsPerDay;

        public int Day { get; private set; } = 1;
        public float Minutes { get; private set; } = DayStart;

        /// <summary>Majukan waktu. True sekali saat hari habis (tengah malam).</summary>
        public bool Update(float dt)
        {
            if (Minutes >= DayEnd) return false;
            Minutes += dt * rate;
            if (Minutes >= DayEnd) { Minutes = DayEnd; return true; }
            return false;
        }

        public void NextDay() { Day++; Minutes = DayStart; }
        public void SkipTo(float minutes) => Minutes = Math.Clamp(minutes, DayStart, DayEnd);

        public string ClockLabel { get { int t = (int)Minutes % (24 * 60); return $"{t / 60:00}:{t % 60 / 10 * 10:00}"; } }
        public string Season => Seasons[(Day - 1) / DaysPerSeason % Seasons.Length];
        public int DayOfSeason => (Day - 1) % DaysPerSeason + 1;
        public int Year => (Day - 1) / (DaysPerSeason * Seasons.Length) + 1;
        public string DateLabel => $"Musim {Season}, Hari {DayOfSeason}";
        public string FullLabel => $"Tahun {Year}, Musim {Season}, Hari {DayOfSeason}";

        public (float R, float G, float B) TintNow()
        {
            float m = Math.Clamp(Minutes, Tint[0].M, Tint[^1].M);
            for (int i = 0; i < Tint.Length - 1; i++)
            {
                var a = Tint[i]; var b = Tint[i + 1];
                if (a.M <= m && m <= b.M)
                {
                    float t = (m - a.M) / (b.M - a.M);
                    return (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
                }
            }
            return (Tint[^1].R, Tint[^1].G, Tint[^1].B);
        }

        public JsonObject ToJson() => new() { ["day"] = Day, ["minutes"] = Minutes };

        public void Load(JsonNode? d)
        {
            if (d == null) return;
            Day = J.I(d["day"]);
            Minutes = J.F(d["minutes"]);
        }
    }
}
