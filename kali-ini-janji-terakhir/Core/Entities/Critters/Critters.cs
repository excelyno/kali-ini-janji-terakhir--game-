using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities.Critters
{
    /// <summary>
    /// ABSTRAKSI: hewan kecil yang berkeliaran di sekitar "rumah"-nya.
    /// Logika jalan-jalan acak ditulis sekali di sini; tiap hewan menentukan gambar
    /// dan perilaku khususnya sendiri.
    /// </summary>
    public abstract class Critter : Entity
    {
        private readonly float homeX, homeY, radius;
        private float targetX, targetY, timer;
        protected readonly Random Rng;

        protected Critter(float x, float y, float speed, float radius, int seed) : base(x, y, speed)
        {
            homeX = targetX = x;
            homeY = targetY = y;
            this.radius = radius;
            Rng = new Random(seed);
        }

        /// <summary>Gerak acak: pilih titik di sekitar rumah, jalan ke sana, diam sebentar.</summary>
        protected void Wander(float dt, FarmWorld world, float speed)
        {
            timer -= dt;
            if (timer <= 0)
            {
                double a = Rng.NextDouble() * Math.PI * 2, r = Rng.NextDouble() * radius;
                targetX = homeX + (float)(Math.Cos(a) * r);
                targetY = homeY + (float)(Math.Sin(a) * r * 0.6);
                timer = 2f + (float)Rng.NextDouble() * 3f;
            }
            float dx = targetX - X, dy = targetY - Y;
            if (dx * dx + dy * dy < 16) { IsMoving = false; return; }
            if (!Move(dx, dy, speed, dt, world)) timer = 0;
        }
    }

    /// <summary>Ayam: berkeliaran, mematuk, kabur kalau pemain berlari di dekatnya, bertelur setiap pagi.</summary>
    public sealed class Chicken : Critter, IInteractable, IDailyUpdatable
    {
        private float fleeTimer, peckTimer, heartTimer;
        private bool facingLeft;
        private readonly Animator animator = new();

        public Chicken(float x, float y, int seed) : base(x, y, 45f, 70f, seed) { }

        public bool HasEgg { get; private set; } = true;

        protected override SizeF FootSize => new(18, 8);
        public override RectangleF Bounds => new(X - 18, Y - 40, 36, 40);

        public override void Update(float dt, FarmWorld world)
        {
            var p = world.Player;
            if (p.IsRunningNow && Distance(p.X, p.Y, X, Y) < 120) fleeTimer = 0.8f;
            if (fleeTimer > 0)
            {
                fleeTimer -= dt;
                Move(X - p.X, Y - p.Y, 150f, dt, world);
            }
            else if (peckTimer > 0)
            {
                peckTimer -= dt;
                IsMoving = false;
            }
            else
            {
                Wander(dt, world, Speed);
                if (!IsMoving && Rng.NextDouble() < dt * 0.4) peckTimer = 0.6f;
            }
            if (heartTimer > 0) heartTimer -= dt;
            if (Facing == Facing.Left) facingLeft = true;
            if (Facing == Facing.Right) facingLeft = false;

            string action = peckTimer > 0 ? "peck" : IsMoving ? "walk" : "idle";
            string side = facingLeft ? "left" : "right";
            animator.Play(Animation.FromArt($"chicken_{action}_{side}", IsMoving ? 8f : 2f));
            animator.Update(dt);
        }

        public void OnNewDay(FarmWorld world) => HasEgg = true;
        public void RestoreEgg(bool hasEgg) => HasEgg = hasEgg;

        public bool InRange(FarmWorld world)
            => Distance(world.Player.X, world.Player.Y, X, Y) < 60 || Bounds.Contains(world.Player.TargetPoint);

        public string Prompt(FarmWorld world) => HasEgg ? "Ambil telur" : "Elus ayam";

        public void Interact(FarmWorld world)
        {
            if (HasEgg)
            {
                HasEgg = false;
                world.Player.Inventory.Add(ItemDb.Egg);
                world.Float("+1 Telur", Color.Beige, X, Y - 50);
                world.Raise(GameEventType.EggCollected);
            }
            heartTimer = 1.2f;
        }

        public override void Draw(Renderer r, float time)
        {
            DrawShadow(r, "m", X, Y);
            r.Draw(animator.Frame ?? Art.Get("chicken_idle_right_0"), X, Y);
            if (heartTimer > 0) r.Draw("ui_bubble_heart", X, Y - 40 - (1.2f - heartTimer) * 12);
            else if (HasEgg) r.Draw("ui_bubble_egg", X, Y - 40 + MathF.Sin(time * 3 + X) * 2);
        }
    }

    /// <summary>Kupu-kupu: terbang mengikuti kurva Lissajous, hanya muncul siang hari yang cerah.</summary>
    public sealed class Butterfly : Critter
    {
        private readonly float baseX, baseY, phase;
        private readonly string color;
        private bool visible;

        public Butterfly(float x, float y, int seed, string color) : base(x, y, 0, 0, seed)
        {
            baseX = x;
            baseY = y;
            phase = seed * 1.7f;
            this.color = color;
        }

        protected override bool Collides => false;
        public override float SortY => Y + 60; // terbang di atas benda lain
        public override RectangleF Bounds => new(X - 8, Y - 56, 16, 16);

        public override void Update(float dt, FarmWorld world)
        {
            visible = !world.Clock.IsNight && world.Weather is WeatherKind.Cerah or WeatherKind.Berawan;
            float t = world.Time * 0.6f + phase;
            X = baseX + MathF.Sin(t * 1.3f) * 70;
            Y = baseY + MathF.Sin(t * 2.1f) * 30;
        }

        public override void Draw(Renderer r, float time)
        {
            if (!visible) return;
            int frame = (int)(time * 10 + phase) % 2;
            DrawShadow(r, "s", X, Y);
            r.Draw($"butterfly_{color}_{frame}", X, Y - 40);
        }
    }
}
