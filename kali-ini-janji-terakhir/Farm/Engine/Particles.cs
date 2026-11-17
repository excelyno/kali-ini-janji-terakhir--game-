namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>Satu partikel: sprite kecil yang terbang, jatuh (gravitasi), lalu memudar.</summary>
    public sealed class Particle
    {
        public required Sprite[] Frames { get; init; }
        public float X, Y, VelocityX, VelocityY;
        public float Gravity { get; init; }
        public float Lifetime { get; init; }
        public float Age;
        public bool Fade { get; init; } = true;

        public bool IsDead => Age >= Lifetime;
        public float Progress => Age / Lifetime;
        public Sprite Frame => Frames[Math.Min(Frames.Length - 1, (int)(Progress * Frames.Length))];
    }

    /// <summary>
    /// Sistem partikel: debu langkah, cipratan air, serpihan kayu, daun, kilau panen.
    /// Animasi partikel diambil dari sprite "{nama}_0..n" (dibagi rata sepanjang umurnya).
    /// </summary>
    public sealed class ParticleSystem
    {
        private readonly List<Particle> particles = new();
        private readonly Random random = new(1);

        public int Count => particles.Count;

        public void Emit(string sprite, float x, float y, float vx = 0, float vy = 0, float life = 0.5f, float gravity = 0, bool fade = true)
            => particles.Add(new Particle
            {
                Frames = Art.Frames(sprite),
                X = x, Y = y, VelocityX = vx, VelocityY = vy,
                Lifetime = life, Gravity = gravity, Fade = fade,
            });

        /// <summary>Semburan: beberapa partikel ke arah acak ke atas (serpihan, daun, gumpalan tanah).</summary>
        public void Burst(string sprite, float x, float y, int count, float speed = 120, float life = 0.6f, float gravity = 500)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = -Math.PI / 2 + (random.NextDouble() - 0.5) * 2.2;
                float s = speed * (0.5f + (float)random.NextDouble());
                Emit(sprite, x, y, (float)Math.Cos(angle) * s, (float)Math.Sin(angle) * s, life, gravity);
            }
        }

        public void Update(float dt)
        {
            foreach (var p in particles)
            {
                p.Age += dt;
                p.VelocityY += p.Gravity * dt;
                p.X += p.VelocityX * dt;
                p.Y += p.VelocityY * dt;
            }
            particles.RemoveAll(p => p.IsDead);
        }

        public void Draw(Renderer r)
        {
            foreach (var p in particles)
                r.Draw(p.Frame, p.X, p.Y, p.Fade ? 1f - p.Progress * p.Progress : 1f);
        }
    }
}
