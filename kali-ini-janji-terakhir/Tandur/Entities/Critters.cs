using Tandur.Engine;
using Tandur.World;

namespace Tandur.Entities
{
    /// <summary>Bundel kecil yang dibagikan PlayScene tiap frame: posisi pemain, gelapnya malam, cuaca.</summary>
    public sealed class CritterCtx
    {
        public float Px, Py, Night;
        public bool Raining, Running;
    }

    /// <summary>
    /// ABSTRAKSI hewan latar yang membuat lembah terasa hidup. Tiap subclass punya otak sendiri di
    /// Update() dan gaya gambar sendiri di Draw() (PEWARISAN + POLIMORFISME). Tidak pernah menghalangi pemain.
    /// </summary>
    public abstract class Critter : Actor
    {
        protected readonly Rng Rng;
        protected string State = "idle";
        protected float Timer, T;
        protected (float X, float Y)? Target;
        protected bool FacingLeft;
        protected bool hidden;

        protected Critter(float gx, float gy, float radius, int seed)
        {
            Rng = new Rng(seed);
            GX = gx; GY = gy;
            HomePos = (gx, gy);
            Radius = radius;
            Timer = Rng.Uniform(0.5f, 2.5f);
            T = Rng.Uniform(0, 10);
        }

        protected virtual float Speed => 0.55f;
        public (float X, float Y) HomePos { get; }
        public float Radius { get; }
        public override bool Hidden => hidden;
        public virtual string? Pettable => null;

        public abstract void Update(float dt, WorldMap map, CritterCtx? ctx);

        protected static bool Ok(WorldMap m, float x, float y) => m.IsWalkable(x, y) && m.HeightAt(MathX.Round(x), MathX.Round(y)) == 0;

        protected bool Step(float dt, WorldMap map, float? speed = null)
        {
            var (tx, ty) = Target!.Value;
            float dx = tx - GX, dy = ty - GY, dist = MathX.Hypot(dx, dy), step = (speed ?? Speed) * dt;
            if (dist < Math.Max(step, 0.05f)) return false;
            float nx = GX + dx / dist * step, ny = GY + dy / dist * step;
            if (!Ok(map, nx, ny)) return false;
            GX = nx; GY = ny;
            float sx = dx - dy;
            if (MathF.Abs(sx) > 1e-3f) FacingLeft = sx < 0;
            return true;
        }

        protected void PickTarget(float? radius = null)
        {
            float r = radius ?? Radius;
            Target = (HomePos.X + Rng.Uniform(-r, r), HomePos.Y + Rng.Uniform(-r, r));
        }

        protected void Put(SpriteBatch b, Atlas a, string name, float yOff = 0, Rgba? color = null)
        {
            var (wx, wy) = WorldPosition;
            b.Sprite(a[name], MathF.Round(wx), MathF.Round(wy - yOff), color, FacingLeft);
        }

        public static Critter Make(string kind, float gx, float gy, float radius, int seed) => kind switch
        {
            "butterfly" => new Butterfly(gx, gy, radius, seed),
            "bird" => new Bird(gx, gy, radius, seed),
            "duck" => new Duck(gx, gy, radius, seed),
            "goat" => new Goat(gx, gy, radius, seed),
            "cat" => new Pet(gx, gy, radius, seed, "cat"),
            "dog" => new Pet(gx, gy, radius, seed, "dog"),
            _ => new Chicken(gx, gy, radius, seed),
        };

        public static bool Floating(string kind) => kind == "duck";
        public static bool Airborne(string kind) => kind == "butterfly";
    }

    /// <summary>Mematuk dan berkeliling. Lari kalau kau berlari lewat. Ayam "lepas" (event cerita) menjauhimu.</summary>
    public sealed class Chicken : Critter
    {
        public Chicken(float gx, float gy, float r, int seed, bool loose = false) : base(gx, gy, r, seed) => Loose = loose;

        public bool Loose { get; }
        public override string? Pettable => Loose ? "Tangkap" : null;

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt; Timer -= dt;
            if (ctx != null)
            {
                float d = MathX.Hypot(ctx.Px - GX, ctx.Py - GY);
                bool scared = d < (Loose ? 2.4f : 1.3f) && (Loose || ctx.Running);
                if (scared && State != "flee")
                {
                    float away = MathF.Atan2(GY - ctx.Py, GX - ctx.Px) + Rng.Uniform(-0.7f, 0.7f);
                    float run = Loose ? 2.2f : 1.6f;
                    Target = (GX + MathF.Cos(away) * run, GY + MathF.Sin(away) * run);
                    State = "flee"; Timer = 1.1f;
                }
            }
            if (State == "flee")
            {
                if (!Step(dt, map, Loose ? 2.3f : 1.7f) || Timer <= 0) { State = "idle"; Timer = Rng.Uniform(0.4f, 1.2f); }
            }
            else if (State == "walk")
            {
                if (!Step(dt, map)) { State = "idle"; Timer = Rng.Uniform(0.8f, 3f); }
            }
            else if (Timer <= 0)
            {
                if (State == "idle" && Rng.Random() < 0.55) { State = "peck"; Timer = Rng.Uniform(0.8f, 1.6f); }
                else if (Rng.Random() < 0.6 && !Loose) { PickTarget(); State = "walk"; }
                else { State = "idle"; Timer = Rng.Uniform(0.8f, 2.5f); }
            }
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            string name = State is "walk" or "flee" ? $"chicken_walk_{(int)(T * (State == "flee" ? 12 : 6)) % 2}"
                : State == "peck" ? ((int)(T * 4) % 2 == 0 ? "chicken_peck" : "chicken_idle") : "chicken_idle";
            Put(b, a, name);
        }
    }

    /// <summary>Kupu-kupu berputar malas di atas bunga. Hanya siang dan saat kering.</summary>
    public sealed class Butterfly : Critter
    {
        private static readonly Rgba[] Colors = { new(1f, 0.92f, 0.45f), new(1f, 0.62f, 0.72f), new(0.66f, 0.84f, 1f), new(1, 1, 1), new(0.98f, 0.70f, 0.36f) };
        private readonly Rgba color;
        private readonly float p1, p2, k;
        private float h = 10;

        public Butterfly(float gx, float gy, float r, int seed) : base(gx, gy, r, seed)
        {
            color = Colors[seed % Colors.Length];
            p1 = Rng.Uniform(0, 6.28f); p2 = Rng.Uniform(0, 6.28f); k = Rng.Uniform(0.7f, 1.2f);
        }

        public override float Depth => GX + GY + 0.6f;

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt * k;
            hidden = ctx != null && (ctx.Night > 0.3f || ctx.Raining);
            float x = HomePos.X + Radius * MathF.Sin(T * 0.43f + p1) * MathF.Cos(T * 0.19f);
            float y = HomePos.Y + Radius * 0.8f * MathF.Sin(T * 0.31f + p2);
            FacingLeft = (x - GX) - (y - GY) < 0;
            GX = x; GY = y;
            h = 11 + 5 * MathF.Sin(T * 2.1f + p1) + 2 * MathF.Sin(T * 9f);
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            if (hidden) return;
            Put(b, a, $"butterfly_{(int)(T * 9) % 2}", h, color);
        }
    }

    /// <summary>Melompat dan mematuk; terbang saat kau mendekat, lalu mendarat di tempat lain.</summary>
    public sealed class Bird : Critter
    {
        private (float X0, float Y0, float X1, float Y1, float Dur) fly;
        private float flyT, h;

        public Bird(float gx, float gy, float r, int seed) : base(gx, gy, r, seed) { }

        public override float Depth => GX + GY + (State == "fly" ? 2f : 0f);

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt; Timer -= dt;
            hidden = ctx != null && ctx.Night > 0.55f;
            if (hidden) return;
            if (State == "fly")
            {
                flyT += dt;
                float p = Math.Min(1, flyT / fly.Dur), e = p * p * (3 - 2 * p);
                GX = fly.X0 + (fly.X1 - fly.X0) * e; GY = fly.Y0 + (fly.Y1 - fly.Y0) * e;
                h = MathF.Sin(p * MathF.PI) * (22 + fly.Dur * 6);
                if (p >= 1) { h = 0; State = "idle"; Timer = Rng.Uniform(0.5f, 2f); }
                return;
            }
            bool near = ctx != null && MathX.Hypot(ctx.Px - GX, ctx.Py - GY) < (ctx.Running ? 3.2f : 2.2f);
            if (near || (Timer <= 0 && Rng.Random() < 0.12))
            {
                for (int i = 0; i < 8; i++)
                {
                    PickTarget(Radius * (near ? 1.6f : 1f));
                    var (tx, ty) = Target!.Value;
                    bool far = ctx == null || MathX.Hypot(ctx.Px - tx, ctx.Py - ty) > 3f;
                    if (Ok(map, tx, ty) && far)
                    {
                        float dist = MathX.Hypot(tx - GX, ty - GY);
                        fly = (GX, GY, tx, ty, Math.Max(0.7f, dist / 4.5f)); flyT = 0; State = "fly";
                        FacingLeft = (tx - GX) - (ty - GY) < 0;
                        return;
                    }
                }
                Timer = 0.6f;
            }
            else if (Timer <= 0)
            {
                if (State == "idle") { State = "peck"; Timer = Rng.Uniform(0.6f, 1.4f); }
                else
                {
                    if (Rng.Random() < 0.5)
                    {
                        float nx = GX + Rng.Uniform(-0.4f, 0.4f), ny = GY + Rng.Uniform(-0.4f, 0.4f);
                        if (Ok(map, nx, ny)) { FacingLeft = (nx - GX) - (ny - GY) < 0; GX = nx; GY = ny; }
                    }
                    State = "idle"; Timer = Rng.Uniform(0.5f, 1.8f);
                }
            }
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            if (hidden) return;
            string name;
            if (State == "fly")
            {
                var (wx, wy) = WorldPosition;
                b.Rect(MathF.Round(wx) - 2, MathF.Round(wy) - 1, 5, 2, new Rgba(0.04f, 0.08f, 0.06f, 0.25f));
                name = $"bird_fly_{(int)(T * 10) % 2}";
            }
            else name = State == "peck" && (int)(T * 5) % 2 == 0 ? "bird_peck" : "bird_idle";
            Put(b, a, name, h);
        }
    }

    /// <summary>Mengapung berkeliling di atas air, meninggalkan riak.</summary>
    public sealed class Duck : Critter
    {
        public Duck(float gx, float gy, float r, int seed) : base(gx, gy, r, seed) { }

        protected override float Speed => 0.38f;
        public override float Depth => GX + GY - 0.4f;

        private static bool Wet(WorldMap m, float x, float y)
        {
            int tx = MathX.Round(x), ty = MathX.Round(y);
            return m.KindAt(tx, ty) == "water" && m.HeightAt(tx, ty) == 0;
        }

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt; Timer -= dt;
            if (ctx != null && ctx.Night > 0.6f) return;
            if (State == "walk")
            {
                var (tx, ty) = Target!.Value;
                float dx = tx - GX, dy = ty - GY, dist = MathX.Hypot(dx, dy), step = Speed * dt, dd = dist == 0 ? 1 : dist;
                float nx = GX + dx / dd * step, ny = GY + dy / dd * step;
                if (dist < step || !Wet(map, nx + 0.35f * (dx / dd), ny + 0.35f * (dy / dd))) { State = "idle"; Timer = Rng.Uniform(1, 4); }
                else
                {
                    GX = nx; GY = ny;
                    if (MathF.Abs(dx - dy) > 1e-3f) FacingLeft = dx - dy < 0;
                }
            }
            else if (Timer <= 0)
            {
                PickTarget();
                if (Wet(map, Target!.Value.X, Target.Value.Y)) State = "walk"; else Timer = 0.4f;
            }
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            var (wx, wy) = WorldPosition;
            int ring = (int)(T * 3) % 4;
            b.Sprite(a[$"ripple_{ring}"], MathF.Round(wx), MathF.Round(wy) + 3, new Rgba(1, 1, 1, 0.5f));
            float bob = MathF.Round(MathF.Sin(T * 2.2f) * 0.6f);
            b.Sprite(a[$"duck_{(int)(T * 2) % 2}"], MathF.Round(wx), MathF.Round(wy) + 2 + bob, flipX: FacingLeft);
        }
    }

    /// <summary>Merumput di dalam kandangnya.</summary>
    public sealed class Goat : Critter
    {
        public Goat(float gx, float gy, float r, int seed) : base(gx, gy, r, seed) { }
        protected override float Speed => 0.45f;

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt; Timer -= dt;
            if (State == "walk")
            {
                if (!Step(dt, map)) { State = "graze"; Timer = Rng.Uniform(2, 5); }
            }
            else if (Timer <= 0)
            {
                if (Rng.Random() < 0.5 && !(ctx != null && ctx.Night > 0.6f)) { PickTarget(); State = "walk"; }
                else { State = Rng.Choice(new[] { "graze", "idle" }); Timer = Rng.Uniform(1.5f, 4); }
            }
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            string name = State == "walk" ? $"goat_walk_{(int)(T * 5) % 2}" : State == "graze" ? $"goat_{((int)(T * 1.5f) % 3 != 0 ? 1 : 0)}" : "goat_0";
            Put(b, a, name);
        }
    }

    /// <summary>Kucing dan anjing desa. Anjing mengikutimu sebentar setelah dielus.</summary>
    public sealed class Pet : Critter
    {
        private float friend, happy;

        public Pet(float gx, float gy, float r, int seed, string kind) : base(gx, gy, r, seed) => Kind = kind;

        public string Kind { get; }
        protected override float Speed => Kind == "cat" ? 0.8f : 1f;
        public override string? Pettable => "Elus";

        public void PetIt()
        {
            happy = 1.2f;
            if (Kind == "dog") friend = 90;
            State = "idle"; Timer = 1.5f;
        }

        public override void Update(float dt, WorldMap map, CritterCtx? ctx)
        {
            T += dt; Timer -= dt;
            happy = Math.Max(0, happy - dt);
            friend = Math.Max(0, friend - dt);
            if (Kind == "dog" && friend > 0 && ctx != null)
            {
                float d = MathX.Hypot(ctx.Px - GX, ctx.Py - GY);
                if (d > 12) friend = 0;
                else if (d > 1.7f)
                {
                    Target = (ctx.Px, ctx.Py);
                    State = Step(dt, map, d > 4 ? 2.3f : 1.4f) ? "walk" : "idle";
                    return;
                }
                else
                {
                    State = "idle";
                    FacingLeft = (ctx.Px - GX) - (ctx.Py - GY) < 0;
                    return;
                }
            }
            if (State == "walk")
            {
                if (!Step(dt, map)) { State = "idle"; Timer = Rng.Uniform(2, Kind == "cat" ? 7 : 4); }
            }
            else if (Timer <= 0)
            {
                bool asleep = ctx != null && ctx.Night > 0.6f;
                if (Rng.Random() < (Kind == "cat" ? 0.35 : 0.6) && !asleep) { PickTarget(); State = "walk"; }
                else { State = Rng.Choice(new[] { "idle", "sniff" }); Timer = Rng.Uniform(1.5f, 5); }
            }
        }

        public override void Draw(SpriteBatch b, Atlas a)
        {
            string name = State == "walk" ? $"{Kind}_walk_{(int)(T * 7) % 2}" : Kind == "cat" ? "cat_sit"
                : State == "sniff" && (int)(T * 2) % 2 == 1 ? "dog_sniff" : "dog_idle";
            float hop = happy > 0 ? MathF.Round(MathF.Abs(MathF.Sin(happy * 9)) * 3) : 0;
            Put(b, a, name, hop);
        }
    }
}
