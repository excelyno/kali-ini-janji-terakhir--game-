using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace Tandur.Engine
{
    /// <summary>Proyeksi isometrik 2:1. Ruang grid: pusat tile di bilangan bulat. Ruang dunia: piksel seni, y ke bawah.</summary>
    public static class Iso
    {
        public const int TileW = 32, TileH = 16;

        public static (float X, float Y) ToWorld(float gx, float gy) => ((gx - gy) * TileW / 2f, (gx + gy) * TileH / 2f);

        public static (float X, float Y) ToGrid(float wx, float wy) => (wx / TileW + wy / TileH, wy / TileH - wx / TileW);
    }

    /// <summary>Kamera 2D dalam piksel dunia. (X, Y) = titik dunia di tengah layar.</summary>
    public sealed class Camera
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public (float X0, float X1, float Y0, float Y1)? Bounds { get; set; }

        public void SnapTo(float x, float y) { X = x; Y = y; }

        public void Follow(float tx, float ty, float smoothing, float dt)
        {
            float t = 1f - MathF.Exp(-smoothing * dt);
            X += (tx - X) * t;
            Y += (ty - Y) * t;
        }

        public void Clamp(float vw, float vh)
        {
            if (Bounds is not { } b) return;
            float hw = vw / 2, hh = vh / 2;
            X = b.X1 - b.X0 <= vw ? (b.X0 + b.X1) / 2 : Math.Clamp(X, b.X0 + hw, b.X1 - hw);
            Y = b.Y1 - b.Y0 <= vh ? (b.Y0 + b.Y1) / 2 : Math.Clamp(Y, b.Y0 + hh, b.Y1 - hh);
        }

        /// <summary>Dibulatkan ke piksel seni utuh agar pixel art tidak berkilau.</summary>
        public (float X, float Y) PixelPosition => (MathF.Round(X), MathF.Round(Y));

        public (float X, float Y) WorldToView(float wx, float wy)
        {
            var (px, py) = PixelPosition;
            return (wx - px, wy - py);
        }
    }

    public enum InputAction { Left, Right, Up, Down, Run, Interact, Confirm, Use, Cancel, Inventory, Journal, Save, Quests, Skip, Debug }

    /// <summary>Input berbasis aksi: gameplay menanyakan aksi, bukan tombol mentah.</summary>
    public sealed class InputState
    {
        private static readonly Dictionary<InputAction, Keys[]> Bindings = new()
        {
            [InputAction.Left] = new[] { Keys.A, Keys.Left },
            [InputAction.Right] = new[] { Keys.D, Keys.Right },
            [InputAction.Up] = new[] { Keys.W, Keys.Up },
            [InputAction.Down] = new[] { Keys.S, Keys.Down },
            [InputAction.Run] = new[] { Keys.LeftShift, Keys.RightShift },
            [InputAction.Interact] = new[] { Keys.E },
            [InputAction.Confirm] = new[] { Keys.Space, Keys.Enter, Keys.KeyPadEnter, Keys.E },
            [InputAction.Use] = new[] { Keys.Space, Keys.J },
            [InputAction.Cancel] = new[] { Keys.Escape },
            [InputAction.Inventory] = new[] { Keys.G, Keys.Tab, Keys.I },
            [InputAction.Journal] = new[] { Keys.C },
            [InputAction.Save] = new[] { Keys.F5 },
            [InputAction.Quests] = new[] { Keys.Q },
            [InputAction.Skip] = new[] { Keys.LeftControl, Keys.RightControl },
            [InputAction.Debug] = new[] { Keys.F3 },
        };

        private readonly HashSet<Keys> held = new(), pressed = new();

        public bool CursorSeen { get; private set; }
        public (float X, float Y) Cursor { get; private set; }
        public bool Clicked { get; private set; }
        public bool MouseDown { get; private set; }
        public int Scroll { get; private set; }

        public void KeyDown(Keys k, bool repeat)
        {
            if (repeat) return;
            held.Add(k);
            pressed.Add(k);
        }

        public void KeyUp(Keys k) => held.Remove(k);
        public void MouseMove(float x, float y) { Cursor = (x, y); CursorSeen = true; }
        public void MouseButton(bool down) { MouseDown = down; if (down) Clicked = true; }
        public void Wheel(float dy) => Scroll += dy > 0 ? 1 : dy < 0 ? -1 : 0;
        public void LostFocus() => held.Clear();

        public bool Held(InputAction a) => Bindings[a].Any(held.Contains);
        public bool Pressed(InputAction a) => Bindings[a].Any(pressed.Contains);
        public bool KeyPressed(Keys k) => pressed.Contains(k);

        public int DigitPressed()
        {
            for (int n = 1; n <= 9; n++) if (pressed.Contains(Keys.D0 + n) || pressed.Contains(Keys.KeyPad0 + n)) return n;
            return 0;
        }

        public void EndStep()
        {
            pressed.Clear();
            Clicked = false;
            Scroll = 0;
        }
    }

    /// <summary>Partikel piksel kecil (debu tanah, tetes air, kilau panen) dan kepulan asap.</summary>
    public sealed class Particles
    {
        private sealed class P { public float X, Y, Vx, Vy, Life, MaxLife, Size; public Rgba C; }
        private sealed class Puff { public float X, Y, Age, Life, Drift; }

        private readonly Random rng = new(7);
        private readonly List<P> items = new();
        private readonly List<Puff> puffs = new();

        private float U(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public void AddPuff(float x, float y, float life = 2.6f) => puffs.Add(new Puff { X = x, Y = y, Life = life * U(0.8f, 1.2f), Drift = U(2, 7) });

        public void Burst(float x, float y, Rgba color, int n = 8, float speed = 26, float life = 0.5f, float up = 22, float size = 2)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = U(0, MathF.Tau), sp = U(0.3f, 1f) * speed;
                items.Add(new P { X = x, Y = y, Vx = MathF.Cos(ang) * sp, Vy = MathF.Sin(ang) * sp * 0.5f - up, Life = life * U(0.7f, 1.2f), MaxLife = life, C = color, Size = size });
            }
        }

        public void Update(float dt, float gravity = 90)
        {
            foreach (var p in items)
            {
                p.Life -= dt;
                p.Vx *= 1 - Math.Min(1, dt * 2);
                p.Vy += gravity * dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
            }
            items.RemoveAll(p => p.Life <= 0);
            foreach (var s in puffs) { s.Age += dt; s.Y -= 9 * dt; s.X += s.Drift * dt; }
            puffs.RemoveAll(s => s.Age >= s.Life);
        }

        public void Draw(SpriteBatch batch)
        {
            foreach (var p in items)
                batch.Rect(MathF.Round(p.X), MathF.Round(p.Y), p.Size, p.Size, p.C.WithA(Math.Min(1, p.Life / p.MaxLife * 1.5f)));
            foreach (var s in puffs)
            {
                float k = s.Age / s.Life;
                int size = 2 + (int)(k * 4);
                batch.Rect(MathF.Round(s.X) - size / 2, MathF.Round(s.Y) - size / 2, size, size, new Rgba(0.93f, 0.93f, 0.95f, 0.42f * (1 - k)));
            }
        }
    }

    /// <summary>Kurva easing, pegas, dan penghitung angka bergulir untuk animasi UI.</summary>
    public static class Ease
    {
        public static float Clamp01(float t) => Math.Clamp(t, 0f, 1f);
        public static float OutCubic(float t) { t = Clamp01(t); return 1 - MathF.Pow(1 - t, 3); }
        public static float InCubic(float t) => MathF.Pow(Clamp01(t), 3);
        public static float OutBack(float t, float s = 1.70158f) { t = Clamp01(t) - 1; return 1 + t * t * ((s + 1) * t + s); }
        public static float InOut(float t) { t = Clamp01(t); return 3 * t * t - 2 * t * t * t; }
    }

    public sealed class Spring
    {
        private readonly float k, d;
        public float Value, Target, Vel;

        public Spring(float value = 0, float stiffness = 260, float damping = 19)
        {
            Value = Target = value;
            k = stiffness;
            d = damping;
        }

        public float Update(float dt)
        {
            int steps = Math.Max(1, (int)(dt / 0.008f) + 1);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vel += ((Target - Value) * k - Vel * d) * h;
                Value += Vel * h;
            }
            return Value;
        }

        public void Kick(float impulse) => Vel += impulse;
        public void Snap(float v) { Value = Target = v; Vel = 0; }
    }

    public sealed class Counter
    {
        public float Shown;
        public int Target;
        public readonly Spring Pop = new(0, 300, 14);
        public int Delta;
        public float DeltaAge = 99;

        public void Set(int v)
        {
            if (v == Target) return;
            Delta = v - Target;
            DeltaAge = 0;
            Pop.Kick(v > Target ? 14 : -8);
            Target = v;
        }

        public void Update(float dt)
        {
            float diff = Target - Shown;
            Shown += Math.Abs(diff) > 0.5f ? diff * Math.Min(1, dt * 7) : diff;
            Pop.Update(dt);
            DeltaAge += dt;
        }

        public string Text => ((int)MathF.Round(Shown)).ToString();
    }
}
