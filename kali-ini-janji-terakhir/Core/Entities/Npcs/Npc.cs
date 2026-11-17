using System;
using System.Collections.Generic;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities.Npcs
{
    /// <summary>
    /// ABSTRAKSI: warga desa. Semua warga berjalan di antara titik jadwalnya,
    /// berhenti saat pemain mendekat, dan bisa diajak bicara.
    /// Isi percakapan (CreateDialogue) wajib dibuat oleh tiap warga: POLIMORFISME.
    /// </summary>
    public abstract class Npc : Character, IInteractable
    {
        private const float TalkRange = 72f;
        private const float StopRange = 100f;

        private readonly List<PointF> route;
        private int routeIndex;
        private float waitTimer = 1.5f;
        private float stuckTimer;
        private bool hasNews;

        protected Npc(string name, IEnumerable<PointF> route, string look)
            : base(name, 0, 0, 75f, look)
        {
            this.route = new List<PointF>(route);
            if (this.route.Count == 0) throw new ArgumentException("Warga butuh minimal satu titik.", nameof(route));
            X = this.route[0].X;
            Y = this.route[0].Y;
        }

        /// <summary>Peran singkat, tampil di bawah nama di kotak dialog.</summary>
        public abstract string Role { get; }

        protected abstract Dialogue CreateDialogue(FarmWorld world);

        /// <summary>Tanda seru di atas kepala: ada misi yang menunggu warga ini.</summary>
        protected virtual bool HasNews(FarmWorld world)
            => world.Quests.Active is TalkQuest t && t.Subject == Name;

        public override void Update(float dt, FarmWorld world)
        {
            hasNews = HasNews(world);
            Walk(dt, world);
            Animate(dt);
        }

        private void Walk(float dt, FarmWorld world)
        {
            var p = world.Player;
            if (Distance(p.X, p.Y, X, Y) < StopRange || waitTimer > 0)
            {
                waitTimer = Math.Max(0, waitTimer - dt);
                IsMoving = false;
                return;
            }

            var target = route[routeIndex];
            float dx = target.X - X, dy = target.Y - Y;
            if (MathF.Abs(dx) < 3 && MathF.Abs(dy) < 3)
            {
                routeIndex = (routeIndex + 1) % route.Count;
                waitTimer = 2f + Id % 3;
                IsMoving = false;
                return;
            }

            // jalan per sumbu supaya terlihat menyusuri jalan setapak
            if (MathF.Abs(dx) >= 3) dy = 0; else dx = 0;
            bool moved = Move(dx, dy, Speed, dt, world);
            stuckTimer = moved ? 0 : stuckTimer + dt;
            if (stuckTimer > 1f)
            {
                routeIndex = (routeIndex + 1) % route.Count;   // terhalang lama: lanjut ke titik berikutnya
                stuckTimer = 0;
                waitTimer = 1f;
            }
        }

        // --- IInteractable ---
        public bool InRange(FarmWorld world)
            => Distance(world.Player.X, world.Player.Y, X, Y) < TalkRange || Hitbox.Contains(world.Player.TargetPoint) ||
               Bounds.Contains(world.Player.TargetPoint);

        public string Prompt(FarmWorld world) => "Bicara dengan " + Name;

        public void Interact(FarmWorld world)
        {
            FaceToward(world.Player.X, world.Player.Y);
            IsMoving = false;
            world.Raise(GameEventType.Talked, 1, Name);
            world.ShowDialogue(CreateDialogue(world), this);
        }

        /// <summary>Potret untuk kotak dialog: frame diam menghadap depan.</summary>
        public Sprite Portrait => Art.Get($"{Look}_idle_down_0");

        public override void Draw(Renderer r, float time)
        {
            base.Draw(r, time);
            if (hasNews) r.Draw("ui_bubble_exclaim", X, Y - 92 + MathF.Sin(time * 5) * 3);
        }
    }
}
