using System;
using System.Drawing;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Entities
{
    public enum Facing { Down, Up, Left, Right }

    public static class FacingExtensions
    {
        /// <summary>Nama arah seperti pada nama sprite: down, up, left, right.</summary>
        public static string Name(this Facing f) => f switch
        {
            Facing.Up => "up",
            Facing.Left => "left",
            Facing.Right => "right",
            _ => "down",
        };

        public static Point Step(this Facing f) => f switch
        {
            Facing.Up => new Point(0, -1),
            Facing.Left => new Point(-1, 0),
            Facing.Right => new Point(1, 0),
            _ => new Point(0, 1),
        };

        public static Facing FromVector(float dx, float dy)
            => MathF.Abs(dx) > MathF.Abs(dy) ? (dx < 0 ? Facing.Left : Facing.Right) : (dy < 0 ? Facing.Up : Facing.Down);
    }

    /// <summary>
    /// Benda yang bisa bergerak (pemain, warga, hewan). PEWARISAN: GameObject → Entity.
    /// Menambahkan arah hadap, kecepatan, dan gerak dengan deteksi tabrakan (move and slide).
    /// </summary>
    public abstract class Entity : GameObject
    {
        protected Entity(float x, float y, float speed) : base(x, y) => Speed = speed;

        public float Speed { get; protected set; }
        public Facing Facing { get; protected set; } = Facing.Down;
        public bool IsMoving { get; protected set; }

        public override bool IsStatic => false;

        /// <summary>Apakah entitas ini ditahan oleh tembok/pohon. Kupu-kupu override jadi false.</summary>
        protected virtual bool Collides => true;

        /// <summary>Lebar dan tinggi kotak kaki (hitbox). Hanya kaki yang bertabrakan, bukan kepala.</summary>
        protected virtual SizeF FootSize => new(24, 12);

        public override RectangleF Hitbox => Collides ? FootAt(X, Y) : RectangleF.Empty;

        public RectangleF FootAt(float x, float y) => new(x - FootSize.Width / 2, y - FootSize.Height, FootSize.Width, FootSize.Height);

        /// <summary>Gerak ke arah (dx, dy). Mengembalikan true jika benar-benar berpindah.</summary>
        protected bool Move(float dx, float dy, float speed, float dt, FarmWorld world)
        {
            IsMoving = dx != 0 || dy != 0;
            if (!IsMoving) return false;
            Facing = FacingExtensions.FromVector(dx, dy);

            float len = MathF.Sqrt(dx * dx + dy * dy);
            float stepX = dx / len * speed * dt, stepY = dy / len * speed * dt;
            var moved = Collides ? world.Physics.MoveAndSlide(Hitbox, stepX, stepY, this) : new PointF(stepX, stepY);
            X += moved.X;
            Y += moved.Y;
            return MathF.Abs(moved.X) + MathF.Abs(moved.Y) > 0.01f;
        }

        protected void FaceToward(float tx, float ty) => Facing = FacingExtensions.FromVector(tx - X, ty - Y);
    }
}
