namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>Deretan frame sprite yang diputar dengan kecepatan tertentu (frame per detik).</summary>
    public sealed class Animation
    {
        public Animation(Sprite[] frames, float fps, bool loop = true)
        {
            if (frames.Length == 0) throw new ArgumentException("Animasi butuh minimal satu frame.", nameof(frames));
            Frames = frames;
            Fps = fps;
            Loop = loop;
        }

        private static readonly Dictionary<(string, float, bool), Animation> cache = new();

        /// <summary>
        /// Animasi dari sprite bernama "{prefix}_0", "{prefix}_1", ...
        /// Animasi tidak bisa diubah, jadi satu objek dipakai bersama oleh semua karakter (di-cache).
        /// </summary>
        public static Animation FromArt(string prefix, float fps, bool loop = true)
        {
            var key = (prefix, fps, loop);
            lock (cache)
            {
                if (!cache.TryGetValue(key, out var a)) cache[key] = a = new Animation(Art.Frames(prefix), fps, loop);
                return a;
            }
        }

        public Sprite[] Frames { get; }
        public float Fps { get; }
        public bool Loop { get; }
        public float Duration => Frames.Length / Fps;

        public int IndexAt(float time)
        {
            int i = (int)(time * Fps);
            return Loop ? i % Frames.Length : Math.Min(i, Frames.Length - 1);
        }
    }

    /// <summary>
    /// Pemutar animasi milik satu objek. Play() dengan animasi yang sama tidak mengulang dari awal,
    /// jadi aman dipanggil setiap frame (pola state machine sederhana: idle / walk / use).
    /// </summary>
    public sealed class Animator
    {
        private Animation? current;
        private float time;

        public Animation? Current => current;
        public int FrameIndex => current?.IndexAt(time) ?? 0;
        public Sprite? Frame => current?.Frames[FrameIndex];
        public bool IsFinished => current != null && !current.Loop && time >= current.Duration;

        public void Play(Animation animation, bool restart = false)
        {
            if (ReferenceEquals(animation, current) && !restart) return;
            current = animation;
            time = 0;
        }

        public void Update(float dt) => time += dt;
    }
}
