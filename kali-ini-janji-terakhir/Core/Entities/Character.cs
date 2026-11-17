using System.Drawing;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities
{
    /// <summary>
    /// Karakter berwujud manusia. PEWARISAN: GameObject → Entity → Character → (Player, Npc).
    /// Animasi dipilih dengan state machine kecil: idle / walk / use, dikali 4 arah.
    /// Nama sprite: "{look}_{aksi}_{arah}_{frame}", misalnya "wira_walk_left_2".
    /// </summary>
    public abstract class Character : Entity
    {
        protected readonly Animator Animator = new();

        protected Character(string name, float x, float y, float speed, string look) : base(x, y, speed)
        {
            Name = name;
            Look = look;
        }

        public string Name { get; }

        /// <summary>Awalan nama sprite: "player", "wira", "lastri", "rentenir".</summary>
        public string Look { get; }

        /// <summary>Apakah sedang berlari (animasi jalan diputar lebih cepat).</summary>
        protected virtual bool IsRunning => false;

        /// <summary>Apakah sedang memakai alat (animasi "use").</summary>
        protected virtual bool IsUsingTool => false;

        public override RectangleF Bounds => Art.Get($"{Look}_idle_down_0").BoundsAt(X, Y);

        protected Animation Anim(string action, float fps, bool loop = true)
            => Animation.FromArt($"{Look}_{action}_{Facing.Name()}", fps, loop);

        /// <summary>Pilih animasi sesuai keadaan. Dipanggil tiap frame dari Update turunan.</summary>
        protected void Animate(float dt)
        {
            if (IsUsingTool) Animator.Play(Anim("use", 7f, loop: false));
            else if (IsMoving) Animator.Play(Anim("walk", IsRunning ? 12f : 8f));
            else Animator.Play(Anim("idle", 1.5f));
            Animator.Update(dt);
        }

        public override void Draw(Renderer r, float time)
        {
            DrawShadow(r, "l", X, Y);
            r.Draw(Animator.Frame ?? Art.Get($"{Look}_idle_down_0"), X, Y);
        }
    }
}
