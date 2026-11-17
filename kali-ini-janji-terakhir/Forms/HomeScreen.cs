using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Farm.Ui;

namespace kali_ini_janji_terakhir.Forms
{
    public enum HomeAction { None, NewGame, Continue, Quit }

    /// <summary>
    /// Menu utama. Latar belakangnya adalah dunia kebun sungguhan (FarmWorld + WorldRenderer)
    /// yang hidup: ayam berjalan, kupu-kupu terbang, kamera bergeser pelan.
    /// </summary>
    public sealed class HomeScreen : IDisposable
    {
        private const int ButtonWidth = 360, ButtonHeight = 52, ButtonGap = 62;

        private static readonly string[] Labels = { "Mulai Baru", "Lanjutkan", "Cara Bermain", "Keluar" };

        private static readonly string[] HelpLines =
        {
            "WASD / panah : berjalan  (tahan Shift untuk lari)",
            "Spasi / klik kiri : pakai alat di kotak sasaran",
            "E / klik kanan : bicara, panen, ambil telur, buka toko",
            "1-9 / roda mouse : ganti alat",
            "Arahkan penyiram ke kolam untuk mengisi air",
            "Q : jurnal   Z : tidur di depan rumah   Esc : jeda",
            "",
            "Tanam, siram, panen, jual di kotak pengiriman,",
            "lalu bayar utang ke Pak Rentenir sebelum musim berganti.",
        };

        private readonly bool hasSave = FarmGame.HasSave;
        private readonly FarmWorld backdrop = new();
        private readonly WorldRenderer backdropView;
        private readonly Camera2D camera = new();
        private int vw = 1280, vh = 720, selected, hover = -1;
        private float time;
        private bool showHelp;

        public HomeScreen()
        {
            selected = hasSave ? 1 : 0;
            backdropView = new WorldRenderer(backdrop);
            camera.SnapTo(BackdropTarget, backdrop.Map.PixelSize);
        }

        public HomeAction Action { get; private set; }

        /// <summary>Kamera bergerak pelan bolak-balik di atas ladang.</summary>
        private PointF BackdropTarget => new(backdrop.Map.PixelSize.Width / 2f + MathF.Sin(time * 0.08f) * 380, 560 + MathF.Sin(time * 0.05f) * 120);

        private bool Enabled(int i) => i != 1 || hasSave;

        private Rectangle ButtonRect(int i) => new((int)(vw * 0.07f), (int)(vh * 0.40f) + i * ButtonGap, ButtonWidth, ButtonHeight);

        public void Resize(int w, int h)
        {
            vw = Math.Max(300, w);
            vh = Math.Max(300, h);
            camera.Resize(vw, vh);
        }

        public void Update(float dt)
        {
            time += dt;
            backdrop.Update(dt, simulate: true);
            camera.Follow(BackdropTarget, backdrop.Map.PixelSize, dt);
        }

        // ---------------------------------------------------------------- input
        public void MouseMove(int x, int y)
        {
            hover = -1;
            if (showHelp) return;
            for (int i = 0; i < Labels.Length; i++)
                if (ButtonRect(i).Contains(x, y)) { hover = i; if (Enabled(i)) selected = i; }
        }

        public void Click(int x, int y)
        {
            if (showHelp) { showHelp = false; return; }
            for (int i = 0; i < Labels.Length; i++)
                if (ButtonRect(i).Contains(x, y) && Enabled(i)) { selected = i; Activate(); return; }
        }

        public void KeyDown(Keys k)
        {
            if (showHelp)
            {
                if (k is Keys.Escape or Keys.Enter or Keys.Space) showHelp = false;
                return;
            }
            if (k is Keys.Up or Keys.W) MoveSelection(-1);
            else if (k is Keys.Down or Keys.S) MoveSelection(1);
            else if (k is Keys.Enter or Keys.Space) Activate();
        }

        private void MoveSelection(int d)
        {
            for (int n = 0; n < Labels.Length; n++)
            {
                selected = (selected + d + Labels.Length) % Labels.Length;
                if (Enabled(selected)) return;
            }
        }

        private void Activate()
        {
            switch (selected)
            {
                case 0: Action = HomeAction.NewGame; break;
                case 1: Action = HomeAction.Continue; break;
                case 2: showHelp = true; break;
                case 3: Action = HomeAction.Quit; break;
            }
        }

        // ---------------------------------------------------------------- render
        public void Render(Graphics g)
        {
            var r = new Renderer(g);
            backdropView.Draw(r, camera, showCursor: false);
            r.BeginScreen();
            r.Tint(Color.FromArgb(90, 20, 12, 30), new Rectangle(0, 0, vw, vh));

            // pahlawan di kanan, sedikit naik-turun
            var hero = Art.Get("hero");
            float heroScale = Math.Min(1f, vh * 0.85f / hero.Height);
            float hx = vw * 0.72f, hy = vh - 10 + MathF.Sin(time * 1.6f) * 4;
            if (vw > 900)
                r.DrawStretched(hero, new RectangleF(hx - hero.Width * heroScale / 2, hy - hero.Height * heroScale, hero.Width * heroScale, hero.Height * heroScale));

            float left = vw * 0.07f;
            r.Text("Kali Ini", Fonts.Make(28f), Theme.Gold, left, vh * 0.10f);
            r.Text("Janji Terakhir", Fonts.Title, Theme.Light, left, vh * 0.10f + 34);
            r.Text("Warisi kebun nenek, lunasi utangnya, tepati janji.", Fonts.Body, Theme.Light, left, vh * 0.10f + 92);

            for (int i = 0; i < Labels.Length; i++) DrawButton(r, i);
            r.Text("Atas/Bawah + Enter, atau klik", Fonts.Small, Theme.Light, left, ButtonRect(Labels.Length - 1).Bottom + 16, alpha: 0.8f);

            if (showHelp) DrawHelp(r);
        }

        private void DrawButton(Renderer r, int i)
        {
            var rect = ButtonRect(i);
            bool active = i == selected && Enabled(i);
            int lift = active ? -3 : 0;
            r.DrawFrame(active ? "ui_frame" : "ui_frame_dark", rect.X, rect.Y + lift, rect.Width, rect.Height);
            var color = !Enabled(i) ? Color.Gray : active ? Theme.Ink : Theme.Light;
            if (active) r.Draw("ui_arrow_right", rect.X + 18, rect.Y + 18 + lift);
            r.Text(Labels[i], Fonts.Large, color, rect.X + 44, rect.Y + 15 + lift, shadow: !active);
        }

        private void DrawHelp(Renderer r)
        {
            r.Tint(Theme.Dim, new Rectangle(0, 0, vw, vh));
            int w = Math.Min(680, vw - 40), h = 120 + HelpLines.Length * 26, x = vw / 2 - w / 2, y = vh / 2 - h / 2;
            r.DrawFrame("ui_frame", x, y, w, h);
            r.TextCentered("Cara Bermain", Fonts.Large, Theme.Ink, vw / 2f, y + 24, shadow: false);
            for (int i = 0; i < HelpLines.Length; i++)
                r.Text(HelpLines[i], Fonts.Body, Theme.Ink, x + 34, y + 66 + i * 26, shadow: false);
            r.TextCentered("Enter / Esc / klik untuk kembali", Fonts.Small, Theme.InkSoft, vw / 2f, y + h - 32, shadow: false);
        }

        public void Dispose() => backdropView.Dispose();
    }
}
