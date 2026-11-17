using System;
using System.Collections.Generic;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Economy;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Tools;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities
{
    /// <summary>
    /// Pemain. PEWARISAN: mewarisi gerak dan animasi dari Character.
    /// ENKAPSULASI: energi privat (hanya TrySpendEnergy/RestoreEnergy), daftar alat
    /// hanya bisa dibaca, dan slot aktif dijaga tetap di rentang yang benar.
    /// </summary>
    public sealed class Player : Character
    {
        /// <summary>Lama satu ayunan alat. Selama itu pemain tidak bisa berjalan.</summary>
        public const float ToolDuration = 0.36f;

        private float energy = GameConfig.MaxEnergy;
        private int activeSlot;
        private readonly List<Tool> tools = new();
        private bool running;
        private float toolTimer;
        private Tool? toolInHand;
        private float dustTimer;

        public Player(float x, float y) : base("Kamu", x, y, GameConfig.WalkSpeed, "player")
        {
            Can = new WateringCan();
            tools.Add(new Hoe());
            tools.Add(Can);
            tools.Add(new Axe());
            tools.Add(new Pickaxe());
            foreach (var kind in CropFactory.All) tools.Add(new SeedBag(kind));
        }

        public Wallet Wallet { get; } = new(100);
        public Inventory Inventory { get; } = new();
        public WateringCan Can { get; }

        public float Energy => energy;
        public float EnergyFraction => energy / GameConfig.MaxEnergy;

        public IReadOnlyList<Tool> Tools => tools;
        public int ActiveSlot
        {
            get => activeSlot;
            set => activeSlot = ((value % tools.Count) + tools.Count) % tools.Count;
        }
        public Tool ActiveTool => tools[activeSlot];

        protected override bool IsRunning => running;
        protected override bool IsUsingTool => toolTimer > 0;
        public bool IsRunningNow => running && IsMoving;
        public bool IsBusy => toolTimer > 0;

        // ---------------------------------------------------------------- sasaran (tile cursor)
        /// <summary>Tile tempat pemain berdiri.</summary>
        public Point Tile => TileMap.TileAt(X, Y - 6);

        /// <summary>Tile yang akan dikenai alat / tombol E.</summary>
        public Point TargetTile { get; private set; }

        public PointF TargetPoint => TileMap.TileCenter(TargetTile);

        /// <summary>
        /// Seperti Stardew Valley: jika mouse menunjuk tile tepat di sekitar pemain, itulah sasarannya.
        /// Jika tidak, sasarannya tile di depan wajah pemain.
        /// </summary>
        public void Aim(Point? mouseTile)
        {
            var me = Tile;
            if (mouseTile is { } m && m != me && Math.Abs(m.X - me.X) <= 1 && Math.Abs(m.Y - me.Y) <= 1)
            {
                TargetTile = m;
                return;
            }
            var step = Facing.Step();
            TargetTile = new Point(me.X + step.X, me.Y + step.Y);
        }

        // ---------------------------------------------------------------- energi
        public bool TrySpendEnergy(float amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (energy < amount) return false;
            energy -= amount;
            return true;
        }

        public void RestoreEnergy(float amount) => energy = Math.Clamp(energy + amount, 0, GameConfig.MaxEnergy);

        // ---------------------------------------------------------------- aksi
        /// <summary>Ayunkan alat aktif ke tile sasaran. Mengembalikan true jika alat benar-benar dipakai.</summary>
        public bool UseActiveTool(FarmWorld world)
        {
            if (IsBusy) return false;
            FaceTile(TargetTile);
            var tool = ActiveTool;
            if (!tool.Use(world, this)) return false;
            toolInHand = tool;
            toolTimer = ToolDuration;
            IsMoving = false;
            Animator.Play(Anim("use", 2 / ToolDuration * 1.1f, loop: false), restart: true);
            return true;
        }

        /// <summary>Gerak dari input keyboard. Saat mengayun alat, pemain diam.</summary>
        public void Control(int dx, int dy, bool run, float dt, FarmWorld world)
        {
            running = run;
            if (IsBusy) { IsMoving = false; return; }
            Move(dx, dy, run ? GameConfig.RunSpeed : GameConfig.WalkSpeed, dt, world);
        }

        public override void Update(float dt, FarmWorld world)
        {
            if (toolTimer > 0) toolTimer -= dt;
            if (IsRunningNow && (dustTimer -= dt) <= 0)
            {
                dustTimer = 0.18f;
                world.Effects.Emit("fx_dust", X, Y - 2, vy: -10, life: 0.4f);
            }
            if (IsBusy) Animator.Update(dt);
            else Animate(dt);
        }

        public void StopMoving() => IsMoving = false;

        public void Teleport(float x, float y)
        {
            X = x;
            Y = y;
        }

        public void RestoreState(float x, float y, float savedEnergy)
        {
            Teleport(x, y);
            energy = Math.Clamp(savedEnergy, 0, GameConfig.MaxEnergy);
        }

        private void FaceTile(Point t)
        {
            var me = Tile;
            if (t != me) Facing = FacingExtensions.FromVector(t.X - me.X, t.Y - me.Y);
        }

        // ---------------------------------------------------------------- gambar
        public override void Draw(Renderer r, float time)
        {
            var swing = SwingSprite();
            bool toolBehind = Facing == Facing.Up;
            if (swing != null && toolBehind) r.Draw(swing, X + HandOffset.X, Y + HandOffset.Y);
            base.Draw(r, time);
            if (swing != null && !toolBehind) r.Draw(swing, X + HandOffset.X, Y + HandOffset.Y);
        }

        /// <summary>Posisi tangan relatif ke kaki, per arah hadap (piksel layar).</summary>
        private Point HandOffset => Facing switch
        {
            Facing.Up => new Point(-6, -44),
            Facing.Left => new Point(-10, -34),
            Facing.Right => new Point(10, -34),
            _ => new Point(12, -32),
        };

        /// <summary>Frame alat yang sedang diayun (3 frame: angkat, ayun, hantam).</summary>
        private Sprite? SwingSprite()
        {
            if (!IsBusy || toolInHand?.SwingName is not { } name) return null;
            float progress = 1f - toolTimer / ToolDuration;
            int frame = Math.Min(2, (int)(progress * 3));
            return Art.Get($"swing_{name}_{Facing.Name()}_{frame}");
        }
    }
}
