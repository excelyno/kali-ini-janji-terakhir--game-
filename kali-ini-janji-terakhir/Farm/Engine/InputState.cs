using System.Drawing;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>
    /// Keadaan keyboard dan mouse untuk satu frame.
    ///   IsHeld     : tombol sedang ditahan (untuk gerak).
    ///   WasPressed : tombol baru ditekan di frame ini (untuk aksi sekali tekan).
    /// Event WinForms hanya mencatat; game membaca di Update lalu memanggil EndFrame().
    /// </summary>
    public sealed class InputState
    {
        private readonly HashSet<Keys> held = new();
        private readonly HashSet<Keys> pressed = new();

        public Point Mouse { get; private set; } = new(-1, -1);
        public bool LeftClicked { get; private set; }
        public bool RightClicked { get; private set; }
        public bool LeftHeld { get; private set; }
        public int WheelSteps { get; private set; }

        // ---------------------------------------------------------------- dicatat dari event
        public void KeyDown(Keys key)
        {
            if (held.Add(key)) pressed.Add(key);
        }

        public void KeyUp(Keys key) => held.Remove(key);
        public void MouseMove(Point p) => Mouse = p;

        public void MouseDown(MouseButtons button)
        {
            if (button == MouseButtons.Left) { LeftClicked = true; LeftHeld = true; }
            if (button == MouseButtons.Right) RightClicked = true;
        }

        public void MouseUp(MouseButtons button)
        {
            if (button == MouseButtons.Left) LeftHeld = false;
        }

        public void Wheel(int delta) => WheelSteps += delta > 0 ? -1 : 1;

        // ---------------------------------------------------------------- dibaca oleh game
        public bool IsHeld(params Keys[] keys) => keys.Any(held.Contains);
        public IReadOnlyCollection<Keys> PressedKeys => pressed.ToList();
        public bool WasPressed(params Keys[] keys) => keys.Any(pressed.Contains);

        /// <summary>Arah gerak dari WASD / panah: masing-masing -1, 0, atau 1.</summary>
        public (int X, int Y) MoveAxis
        {
            get
            {
                int x = (IsHeld(Keys.D, Keys.Right) ? 1 : 0) - (IsHeld(Keys.A, Keys.Left) ? 1 : 0);
                int y = (IsHeld(Keys.S, Keys.Down) ? 1 : 0) - (IsHeld(Keys.W, Keys.Up) ? 1 : 0);
                return (x, y);
            }
        }

        /// <summary>Tombol 1..9 yang baru ditekan (0..8), atau -1.</summary>
        public int PressedNumber()
        {
            for (int i = 0; i < 9; i++)
                if (pressed.Contains(Keys.D1 + i)) return i;
            return -1;
        }

        /// <summary>Hapus kejadian "sekali tekan" setelah satu update.</summary>
        public void EndFrame()
        {
            pressed.Clear();
            LeftClicked = RightClicked = false;
            WheelSteps = 0;
        }

        /// <summary>Saat jendela kehilangan fokus: lepaskan semua tombol.</summary>
        public void Clear()
        {
            held.Clear();
            EndFrame();
            LeftHeld = false;
        }
    }
}
