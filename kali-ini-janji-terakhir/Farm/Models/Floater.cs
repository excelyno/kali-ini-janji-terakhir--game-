using System.Drawing;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>Teks melayang singkat ("+3 Kayu", "Disiram") yang naik lalu memudar.</summary>
    public sealed class Floater
    {
        private const float Lifetime = 1.4f;
        private float life = Lifetime;

        public Floater(string text, Color color, float x, float y)
        {
            Text = text;
            Color = color;
            X = x;
            Y = y;
        }

        public string Text { get; }
        public Color Color { get; }
        public float X { get; }
        public float Y { get; private set; }
        public float Alpha => System.Math.Clamp(life, 0f, 1f);
        public bool IsDead => life <= 0;

        public void Update(float dt)
        {
            life -= dt;
            Y -= 30 * dt;
        }
    }
}
