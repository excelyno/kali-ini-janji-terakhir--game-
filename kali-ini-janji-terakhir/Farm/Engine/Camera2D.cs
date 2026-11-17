using System.Drawing;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>
    /// Kamera 2D: mengikuti target dengan halus (exponential smoothing), tidak keluar dari tepi dunia,
    /// dan posisinya dibulatkan ke piksel agar pixel art tidak "bergetar".
    /// </summary>
    public sealed class Camera2D
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public int ViewWidth { get; private set; } = 1280;
        public int ViewHeight { get; private set; } = 720;

        /// <summary>Semakin besar, semakin cepat kamera menyusul target.</summary>
        public float Smoothing { get; set; } = 8f;

        public RectangleF View => new(X, Y, ViewWidth, ViewHeight);

        public void Resize(int width, int height)
        {
            ViewWidth = Math.Max(1, width);
            ViewHeight = Math.Max(1, height);
        }

        /// <summary>Langsung pindah ke target (dipakai saat mulai / teleport).</summary>
        public void SnapTo(PointF target, SizeF world)
        {
            X = target.X - ViewWidth / 2f;
            Y = target.Y - ViewHeight / 2f;
            Clamp(world);
        }

        public void Follow(PointF target, SizeF world, float dt)
        {
            float k = 1f - MathF.Exp(-Smoothing * dt);   // tidak tergantung frame rate
            X += (target.X - ViewWidth / 2f - X) * k;
            Y += (target.Y - ViewHeight / 2f - Y) * k;
            Clamp(world);
        }

        public PointF ScreenToWorld(Point screen) => new(screen.X + (int)X, screen.Y + (int)Y);

        private void Clamp(SizeF world)
        {
            X = world.Width <= ViewWidth ? -(ViewWidth - world.Width) / 2f : Math.Clamp(X, 0, world.Width - ViewWidth);
            Y = world.Height <= ViewHeight ? -(ViewHeight - world.Height) / 2f : Math.Clamp(Y, 0, world.Height - ViewHeight);
        }
    }
}
