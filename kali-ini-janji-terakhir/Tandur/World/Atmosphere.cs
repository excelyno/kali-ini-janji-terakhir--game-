using Tandur.Core;
using Tandur.Engine;

namespace Tandur.World
{
    /// <summary>Langit dan udara: cuaca, bayangan awan, lampu hangat malam, kunang-kunang, partikel musim.</summary>
    public sealed class Atmosphere
    {
        public static readonly Dictionary<string, string> WeatherNames = new()
        {
            ["clear"] = "Cerah", ["rain"] = "Hujan", ["wind"] = "Berangin", ["fog"] = "Berkabut", ["snow"] = "Salju",
        };
        private static readonly Dictionary<string, (double Rain, double Wind, double Fog)> Odds = new()
        {
            ["Semi"] = (0.28, 0.10, 0.14), ["Panas"] = (0.12, 0.16, 0.04), ["Gugur"] = (0.24, 0.26, 0.14), ["Dingin"] = (0.30, 0.10, 0.18),
        };
        private static readonly Dictionary<string, (float, float, float)> SeasonTint = new()
        {
            ["Semi"] = (1f, 1f, 1f), ["Panas"] = (1.03f, 1f, 0.94f), ["Gugur"] = (1.05f, 0.95f, 0.85f), ["Dingin"] = (0.90f, 0.95f, 1.05f),
        };
        private const float WrapW = 760, WrapH = 460;

        private readonly Atlas atlas;
        private readonly List<float[]> clouds = new(), flies = new(), motes = new();

        public Atmosphere(Atlas atlas, long seed = 1)
        {
            this.atlas = atlas;
            var rng = new Rng(seed);
            for (int i = 0; i < 7; i++) clouds.Add(new[] { rng.Uniform(0, WrapW), rng.Uniform(0, WrapH), i % 3, rng.Uniform(5, 9) });
            for (int i = 0; i < 26; i++) flies.Add(new[] { rng.Uniform(0, WrapW), rng.Uniform(0, WrapH), rng.Uniform(0, 6.28f), rng.Uniform(0.6f, 1.4f) });
            for (int i = 0; i < 90; i++) motes.Add(new[] { (float)rng.Random(), (float)rng.Random(), rng.Uniform(0, 6.28f), rng.Uniform(0.7f, 1.3f) });
        }

        public float T { get; private set; }
        public string Weather { get; private set; } = "clear";
        public string Season { get; private set; } = "Semi";
        public int Act { get; private set; }
        public float Night { get; private set; }
        public float Minutes { get; private set; } = 360;
        public List<(float X, float Y, float Size)> Lights { get; set; } = new();
        public float Fireflies { get; set; }
        public bool Raining => Weather == "rain";
        public bool Drought => Act == 3;

        public static string WeatherFor(long seed, int day, string season, int act)
        {
            if (day <= 1 || act == 5) return "clear";
            if (act == 0) return "fog";
            if (act == 3) return "clear";
            double r = new Rng((seed * 31 + day * 7919L) & 0xFFFFFFFFL).Random();
            var (rain, wind, fog) = Odds.GetValueOrDefault(season, Odds["Semi"]);
            if (r < rain) return season == "Dingin" ? "snow" : "rain";
            if (r < rain + wind) return "wind";
            if (r < rain + wind + fog) return "fog";
            return "clear";
        }

        public string SetDay(long seed, int day, string season, int act)
        {
            Season = season; Act = act;
            Weather = WeatherFor(seed, day, season, act);
            return Weather;
        }

        public Rgba Tint((float R, float G, float B) b)
        {
            var s = SeasonTint.GetValueOrDefault(Season, (1f, 1f, 1f));
            var w = Weather switch { "rain" => (0.80f, 0.86f, 0.96f), "fog" => (0.95f, 0.96f, 0.98f), "snow" => (0.92f, 0.95f, 1.0f), _ => (1f, 1f, 1f) };
            var d = Drought ? (1.04f, 0.93f, 0.70f) : (1f, 1f, 1f);
            return new Rgba(b.R * s.Item1 * w.Item1 * d.Item1, b.G * s.Item2 * w.Item2 * d.Item2, b.B * s.Item3 * w.Item3 * d.Item3, 1);
        }

        private static float FMod(float a, float m) { float r = a % m; return r < 0 ? r + m : r; }

        public void Update(float dt, float minutes)
        {
            T += dt;
            Minutes = minutes;
            Night = Ease.Clamp01((minutes - 18.2f * 60) / 100f);
            float wind = Weather == "wind" ? 2.4f : 1f;
            foreach (var c in clouds) { c[0] = FMod(c[0] + c[3] * wind * dt, WrapW); c[1] = FMod(c[1] + c[3] * 0.35f * wind * dt, WrapH); }
            foreach (var f in flies)
            {
                f[2] += dt * f[3];
                f[0] = FMod(f[0] + MathF.Cos(f[2] * 0.7f) * 9 * dt, WrapW);
                f[1] = FMod(f[1] + MathF.Sin(f[2] * 0.9f) * 6 * dt, WrapH);
            }
        }

        public void DrawWorld(Renderer gfx, Camera camera, (float L, float R, float T, float B) bounds)
        {
            var batch = gfx.Batch;
            var (left, right, top, bottom) = bounds;
            float cx = (left + right) / 2, cy = (top + bottom) / 2;
            float strength = Weather switch { "clear" or "wind" => 1f, "fog" => 0.5f, _ => 0f } * (1 - 0.7f * Night);
            if (strength > 0.02f)
                foreach (var c in clouds)
                {
                    float wx = cx + FMod(c[0] - cx, WrapW) - WrapW / 2, wy = cy + FMod(c[1] - cy, WrapH) - WrapH / 2;
                    batch.Sprite(atlas[$"cloud_{J.I(c[2])}"], MathF.Round(wx), MathF.Round(wy), new Rgba(0.04f, 0.08f, 0.16f, 0.17f * strength));
                }
            if (Night <= 0.01f && Act != 5) return;
            var saved = gfx.Lights;
            gfx.Lights = new();
            gfx.ViewWorld(camera, Rgba.White);                     // halo tidak terkena tint malam
            gfx.Lights = saved;
            var glow = atlas["glow"];
            float n = Math.Max(Night, 0);
            for (int i = 0; i < Lights.Count; i++)
            {
                var (wx, wy, size) = Lights[i];
                if (!(left - 80 < wx && wx < right + 80 && top - 80 < wy && wy < bottom + 80)) continue;
                float flick = 0.9f + 0.1f * MathF.Sin(T * 5 + i * 1.7f);
                batch.Sprite(glow, MathF.Round(wx), MathF.Round(wy), new Rgba(1f, 0.80f, 0.42f, 0.50f * n * flick), scale: size);
                batch.Sprite(glow, MathF.Round(wx), MathF.Round(wy), new Rgba(1f, 0.93f, 0.66f, 0.55f * n * flick), scale: size * 0.42f);
            }
            if (Fireflies > 0.01f && n > 0.2f)
                foreach (var f in flies)
                {
                    float wx = cx + FMod(f[0] - cx, WrapW) - WrapW / 2, wy = cy + FMod(f[1] - cy, WrapH) - WrapH / 2;
                    float a = (0.5f + 0.5f * MathF.Sin(f[2] * 2.3f)) * n * Fireflies;
                    if (a > 0.08f)
                    {
                        batch.Sprite(glow, MathF.Round(wx), MathF.Round(wy), new Rgba(0.78f, 1f, 0.45f, 0.35f * a), scale: 0.22f);
                        batch.Rect(MathF.Round(wx), MathF.Round(wy), 1, 1, new Rgba(0.92f, 1f, 0.62f, a));
                    }
                }
        }

        private string Mode()
        {
            if (Weather is "rain" or "snow") return Weather;
            if (Drought) return "dust";
            if (Act == 5) return "confetti";
            return Season switch { "Panas" => "pollen", "Gugur" => "leaf", "Dingin" => "flake", _ => "petal" };
        }

        public void DrawScreen(Renderer gfx)
        {
            var batch = gfx.Batch;
            var (w, h) = gfx.UiSize;
            float t = T;
            string mode = Mode();
            if (Weather == "rain") batch.Rect(0, 0, w, h, new Rgba(0.10f, 0.14f, 0.24f, 0.16f));
            if (Weather == "fog")
            {
                float a = Act == 0 ? 0.22f : 0.20f * Ease.Clamp01((11.5f * 60 - Minutes) / 150f);
                if (a > 0.01f)
                {
                    batch.Rect(0, 0, w, h, new Rgba(0.86f, 0.90f, 0.94f, a));
                    for (int i = 0; i < 5; i++)
                    {
                        float y = FMod(i * 173 + t * (6 + i), h + 160) - 80;
                        batch.Rect(0, y, w, 70, new Rgba(0.92f, 0.95f, 0.98f, a * 0.5f));
                    }
                }
            }
            if (Drought) batch.Rect(0, 0, w, h, new Rgba(0.96f, 0.74f, 0.32f, 0.09f + 0.02f * MathF.Sin(t * 0.7f)));
            float wind = Weather == "wind" ? 2.6f : 1f;
            int count = mode switch { "rain" => 90, "snow" => 80, "flake" => 34, "petal" => 16, "leaf" => 22, "pollen" => 10, "dust" => 26, _ => 40 };
            if (Weather == "wind") count = Math.Min(motes.Count, count + 18);
            float nightDim = 1 - 0.45f * Night;
            for (int i = 0; i < count; i++)
            {
                float px = motes[i][0], py = motes[i][1], phase = motes[i][2], sp = motes[i][3];
                if (mode == "rain")
                {
                    float y = FMod(py * h + t * 880 * sp, h + 40) - 20;
                    float x = FMod(px * (w + 300) - t * 240 * sp, w + 300) - 150;
                    batch.Rect(x, y, 2, 16, new Rgba(0.72f, 0.84f, 0.98f, 0.42f));
                    batch.Rect(x - 2, y + 16, 2, 8, new Rgba(0.72f, 0.84f, 0.98f, 0.26f));
                    if (y > h - 60 && i % 3 == 0)                 // percikan kecil di bawah layar
                        batch.Rect(x - 3, h - 18 - (i % 5) * 8, 6, 2, new Rgba(0.8f, 0.9f, 1f, 0.3f));
                    continue;
                }
                if (mode is "snow" or "flake")
                {
                    float y = FMod(py * h + t * 46 * sp, h + 20) - 10;
                    float x = FMod(px * w + MathF.Sin(t * 0.9f + phase) * 26 + t * 14 * wind, w + 20) - 10;
                    float size = i % 3 != 0 ? 4 : 6;
                    batch.Rect(x, y, size, size, new Rgba(1, 1, 1, 0.82f * nightDim));
                    continue;
                }
                if (mode == "dust")
                {
                    float y = h - FMod(py * h + t * 16 * sp, h + 20);
                    float x = FMod(px * w + MathF.Sin(t * 0.5f + phase) * 30 + t * 22, w + 20) - 10;
                    batch.Rect(x, y, 3, 3, new Rgba(0.93f, 0.80f, 0.50f, 0.34f));
                    continue;
                }
                float fall = (mode switch { "petal" => 34f, "leaf" => 52f, "pollen" => 10f, _ => 70f }) * sp;
                float yy = FMod(py * h + t * fall, h + 30) - 15;
                float xx = FMod(px * w + MathF.Sin(t * 1.3f * sp + phase) * 38 + t * 30 * wind, w + 30) - 15;
                float flip = MathF.Abs(MathF.Sin(t * 2.6f * sp + phase));
                switch (mode)
                {
                    case "petal":
                        batch.Rect(xx, yy, 6, 2 + 3 * flip, (i % 2 == 1 ? new Rgba(1f, 0.76f, 0.84f) : new Rgba(1f, 0.90f, 0.93f)).WithA(0.80f * nightDim));
                        break;
                    case "leaf":
                        var lc = new[] { new Rgba(0.90f, 0.52f, 0.20f), new Rgba(0.82f, 0.36f, 0.16f), new Rgba(0.93f, 0.72f, 0.26f) }[i % 3];
                        batch.Rect(xx, yy, 7, 2 + 4 * flip, lc.WithA(0.85f * nightDim));
                        break;
                    case "pollen":
                        batch.Rect(xx, yy, 3, 3, new Rgba(1f, 0.96f, 0.66f, 0.5f * nightDim));
                        break;
                    default:
                        var cc = new[] { new Rgba(1f, 0.82f, 0.30f), new Rgba(0.95f, 0.42f, 0.40f), new Rgba(0.50f, 0.82f, 0.95f), new Rgba(0.62f, 0.90f, 0.50f) }[i % 4];
                        batch.Rect(xx, yy, 6, 2 + 5 * flip, cc.WithA(0.9f));
                        break;
                }
            }
        }

        /// <summary>Isi parameter post-processing sesuai suasana (versi C#: lebih sinematik).</summary>
        public void ConfigurePost(PostFx fx)
        {
            fx.Reset();
            float n = Night;
            fx.Bloom = 0.22f + 0.75f * n;
            fx.Threshold = 0.82f - 0.42f * n;
            fx.Vignette = 0.30f + 0.35f * n + (Weather == "fog" ? 0.15f : 0);
            fx.Saturation = Weather switch { "rain" => 0.88f, "fog" => 0.85f, _ => 1.08f } - 0.12f * n;
            fx.Contrast = 1.05f;
            switch (Season)
            {
                case "Gugur": fx.GradeR = 1.03f; fx.GradeB = 0.96f; break;
                case "Dingin": fx.GradeR = 0.97f; fx.GradeB = 1.03f; break;
                case "Panas": fx.GradeR = 1.02f; fx.GradeG = 1.01f; break;
            }
            if (Drought) { fx.Heat = 1f - n; fx.Saturation -= 0.05f; }
            if (Raining) fx.Wet = 1f;
        }
    }
}
