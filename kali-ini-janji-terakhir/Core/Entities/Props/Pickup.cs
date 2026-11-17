using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities.Props
{
    /// <summary>
    /// Barang yang jatuh ke tanah (kayu, batu). Melompat sebentar, lalu tertarik ke pemain
    /// saat dekat ("magnet") dan masuk tas ketika tersentuh, seperti di Stardew Valley.
    /// Ketinggian lompatan (Z) dipisah dari posisi tanah (X, Y) agar bayangan tetap di tanah.
    /// </summary>
    public sealed class Pickup : GameObject
    {
        private const float MagnetRange = 110f;
        private const float CollectRange = 18f;
        private const float Gravity = 900f;

        private float z, vz, vx, vy, age;

        public Pickup(Item item, float x, float y, Random random) : base(x, y)
        {
            Item = item;
            double angle = random.NextDouble() * Math.PI * 2;
            vx = (float)Math.Cos(angle) * 60;
            vy = (float)Math.Sin(angle) * 30;
            vz = 260 + (float)random.NextDouble() * 80;
        }

        public Item Item { get; }

        public override RectangleF Bounds => new(X - 16, Y - 40 - z, 32, 40);

        public override void Update(float dt, FarmWorld world)
        {
            age += dt;
            if (age < 0.6f || z > 0)
            {
                // fase lompat: gerak parabola dengan gravitasi, memantul sekali kecil
                X += vx * dt;
                Y += vy * dt;
                vz -= Gravity * dt;
                z = Math.Max(0, z + vz * dt);
                if (z == 0 && vz < -150) vz = -vz * 0.35f;
                return;
            }

            var p = world.Player;
            float dx = p.X - X, dy = (p.Y - 20) - Y, dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < CollectRange) { world.CollectPickup(this); return; }
            if (dist < MagnetRange)
            {
                float speed = 380 * (1.2f - dist / MagnetRange);   // makin dekat makin cepat
                X += dx / dist * speed * dt;
                Y += dy / dist * speed * dt;
            }
        }

        public override void Draw(Renderer r, float time)
        {
            float bob = z == 0 ? MathF.Sin(time * 4 + X) * 2 : 0;
            DrawShadow(r, "s", X, Y);
            r.DrawCentered(Art.Get(Item.IconName), X, Y - 16 - z - bob);
        }
    }
}
