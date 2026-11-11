using System.Drawing.Drawing2D;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Forms
{
    public enum HomeAction { None, NewGame, Continue, Quit }

    /// <summary>Main menu with the hero section (title, character, Start / Continue / ...).</summary>
    public sealed class HomeScreen : IDisposable
    {
        static readonly Color Accent = Color.FromArgb(255, 226, 168, 60), Cream = Color.FromArgb(255, 250, 238, 205), Panel = Color.FromArgb(215, 38, 28, 20);

        readonly string[] labels = { "Mulai Baru", "Lanjutkan", "Cara Bermain", "Keluar" };
        readonly bool hasSave = FarmGame.HasSave;
        readonly Font fTitle = new("Segoe UI", 46f, FontStyle.Bold), fSub = new("Segoe UI", 13f, FontStyle.Bold),
            fBtn = new("Segoe UI", 16f, FontStyle.Bold), fSmall = new("Segoe UI", 10f, FontStyle.Bold), fKicker = new("Segoe UI", 20f, FontStyle.Bold);
        Bitmap? scene;
        int vw = 1280, vh = 720, sel, hover = -1;
        float anim;
        bool showHelp;

        public HomeAction Action { get; private set; }

        public HomeScreen() { sel = hasSave ? 1 : 0; }

        bool Enabled(int i) => i != 1 || hasSave;

        RectangleF ButtonRect(int i) => new(vw * 0.07f, vh * 0.42f + i * 62, 380, 52);

        public void Resize(int w, int h)
        {
            vw = Math.Max(300, w); vh = Math.Max(300, h);
            scene?.Dispose(); scene = null;
        }

        public void Update(float dt) => anim += dt;

        public void MouseMove(int x, int y)
        {
            hover = -1;
            if (showHelp) return;
            for (int i = 0; i < labels.Length; i++)
                if (ButtonRect(i).Contains(x, y)) { hover = i; if (Enabled(i)) sel = i; }
        }

        public void Click(int x, int y)
        {
            if (showHelp) { showHelp = false; return; }
            for (int i = 0; i < labels.Length; i++)
                if (ButtonRect(i).Contains(x, y) && Enabled(i)) { sel = i; Activate(); return; }
        }

        public void KeyDown(Keys k)
        {
            if (showHelp) { if (k == Keys.Escape || k == Keys.Enter || k == Keys.Space) showHelp = false; return; }
            if (k == Keys.Up || k == Keys.W) Move(-1);
            else if (k == Keys.Down || k == Keys.S) Move(1);
            else if (k == Keys.Enter || k == Keys.Space) Activate();
        }

        void Move(int d)
        {
            for (int n = 0; n < labels.Length; n++)
            {
                sel = (sel + d + labels.Length) % labels.Length;
                if (Enabled(sel)) return;
            }
        }

        void Activate()
        {
            switch (sel)
            {
                case 0: Action = HomeAction.NewGame; break;
                case 1: Action = HomeAction.Continue; break;
                case 2: showHelp = true; break;
                case 3: Action = HomeAction.Quit; break;
            }
        }

        // ---------- rendering ----------
        void RRect(Graphics g, Color fill, float x, float y, float w, float h, float r = 10, Color? border = null, float bw = 2)
        {
            using var p = new GraphicsPath();
            float d = Math.Min(r * 2, Math.Min(w, h));
            p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure();
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
            if (border != null) { using var pen = new Pen(border.Value, bw); g.DrawPath(pen, p); }
        }

        void Txt(Graphics g, string s, Font f, Color c, float x, float y, int shadow = 2)
        {
            using var sh = new SolidBrush(Color.FromArgb(190, 20, 12, 4)); using var b = new SolidBrush(c);
            g.DrawString(s, f, sh, x + shadow, y + shadow); g.DrawString(s, f, b, x, y);
        }

        void Sprite(Graphics g, Bitmap bmp, float cx, float bottom, float scale = 1f)
        {
            float w = bmp.Width * scale, h = bmp.Height * scale;
            g.DrawImage(bmp, cx - w / 2, bottom - h, w, h);
        }

        Bitmap BuildScene()
        {
            var bmp = new Bitmap(vw, vh);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(142, 172, 104));
            var r = new Random(11);
            Color[] gc = { Color.FromArgb(136, 166, 98), Color.FromArgb(148, 178, 110), Color.FromArgb(130, 160, 94) };
            for (int i = 0; i < vw * vh / 260; i++)
            {
                using var b = new SolidBrush(gc[r.Next(3)]);
                g.FillRectangle(b, r.Next(vw), r.Next(vh), 6, 6);
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using (var path = new SolidBrush(Color.FromArgb(200, 175, 120)))
            {
                g.FillRectangle(path, 0, vh * 0.80f, vw, 60);
                g.FillRectangle(path, vw * 0.55f - 33, vh * 0.36f, 66, vh * 0.50f);
            }

            // small tilled field with sprouts, like the goal map
            int fx = (int)(vw * 0.36f), fy = (int)(vh * 0.52f);
            for (int cx = 0; cx < 6; cx++)
                for (int cy = 0; cy < 3; cy++)
                {
                    g.DrawImageUnscaled(cy < 2 ? Sprites.SoilTilled : Sprites.SoilBare, fx + cx * 48, fy + cy * 48);
                    if (cy == 0 && cx < 4) g.DrawImageUnscaled(cx < 2 ? Sprites.Harvest : Sprites.SproutM, fx + cx * 48 + 24 - (cx < 2 ? Sprites.Harvest.Width : Sprites.SproutM.Width) / 2, fy + 40 - (cx < 2 ? Sprites.Harvest.Height : Sprites.SproutM.Height));
                }
            g.DrawImageUnscaled(Sprites.Fence, fx - 24, fy - Sprites.Fence.Height - 2);

            Sprite(g, Sprites.Pond, vw * 0.86f, vh * 0.62f, 0.9f);
            Sprite(g, Sprites.House, vw * 0.55f, vh * 0.36f, 1.15f);
            Sprite(g, Sprites.Shop, vw * 0.22f, vh * 0.36f);
            Sprite(g, Sprites.Crates, vw * 0.22f - 120, vh * 0.35f);
            Sprite(g, Sprites.Rock, vw * 0.30f, vh * 0.78f);
            Sprite(g, Sprites.Rock, vw * 0.68f, vh * 0.93f, 0.8f);
            foreach (var (fxr, fyr) in new[] { (0.03f, 0.55f), (0.10f, 1.02f), (0.38f, 1.03f), (0.64f, 1.04f), (0.93f, 1.02f), (0.97f, 0.30f), (0.03f, 0.12f), (0.82f, 0.20f), (0.45f, 0.18f) })
                Sprite(g, Sprites.Tree, vw * fxr, vh * fyr, 1.1f);

            // readable text area on the left
            using var shade = new LinearGradientBrush(new Rectangle(0, 0, (int)(vw * 0.62f), vh), Color.FromArgb(205, 20, 14, 8), Color.FromArgb(0, 20, 14, 8), LinearGradientMode.Horizontal);
            g.FillRectangle(shade, 0, 0, (int)(vw * 0.62f), vh);
            return bmp;
        }

        public void Render(Graphics g)
        {
            scene ??= BuildScene();
            g.DrawImageUnscaled(scene, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // chickens wandering around the field
            for (int i = 0; i < 2; i++)
            {
                float cx = vw * (0.44f + i * 0.17f) + MathF.Sin(anim * 0.8f + i * 2) * 26, cy = vh * (0.74f + i * 0.04f);
                Sprite(g, Sprites.Chicken, cx, cy + MathF.Sin(anim * 6 + i) * 2, 1.1f);
            }

            // ----- hero section: character with a glow, and tools floating around -----
            float hh = Math.Min(vh * 0.78f, 640), hw = Sprites.Hero.Width * hh / Sprites.Hero.Height;
            float hx = vw * 0.74f, hb = vh * 0.93f + MathF.Sin(anim * 2f) * 6;
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(hx - hw * 0.95f, hb - hh * 0.95f, hw * 1.9f, hh * 0.95f);
                using var pg = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(150, 255, 230, 150), SurroundColors = new[] { Color.FromArgb(0, 255, 230, 150) } };
                g.FillPath(pg, glow);
            }
            using (var sh = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(sh, hx - hw * 0.4f, vh * 0.93f - 14, hw * 0.8f, 28);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(Sprites.Hero, hx - hw / 2, hb - hh, hw, hh);

            var tools = new[] { Sprites.Hoe, Sprites.Axe, Sprites.Pick, Sprites.Can, Sprites.Backpack };
            for (int i = 0; i < tools.Length; i++)
            {
                float a = -2.5f + i * 1.25f / 1f * 0.8f;               // fan of icons around the character
                float tx = hx + MathF.Cos(a - MathF.PI / 2 + 0.0f) * hw * 0.95f * (i % 2 == 0 ? 1f : 1.12f);
                float ty = hb - hh * 0.55f + MathF.Sin(a - MathF.PI / 2) * hh * 0.42f + MathF.Sin(anim * 2 + i) * 5;
                RRect(g, Color.FromArgb(150, 38, 28, 20), tx - 30, ty - 30, 60, 60, 12, Color.FromArgb(160, 226, 168, 60));
                g.DrawImage(tools[i], tx - 20, ty - 20, 40, 40);
            }

            // ----- title block -----
            float tx0 = vw * 0.07f;
            Txt(g, "KEBUN PENUH HARAPAN", fSmall, Accent, tx0 + 4, vh * 0.10f);
            Txt(g, "Kali Ini", fKicker, Cream, tx0 + 2, vh * 0.135f);
            Txt(g, "Janji Terakhir", fTitle, Accent, tx0, vh * 0.17f, 3);
            Txt(g, $"Garap lahan, tanam, panen, dan kumpulkan {FarmGame.GoalGold} emas!", fSub, Cream, tx0 + 4, vh * 0.17f + 82);

            // ----- menu -----
            for (int i = 0; i < labels.Length; i++)
            {
                var b = ButtonRect(i);
                bool en = Enabled(i), on = i == sel && en;
                RRect(g, on ? Color.FromArgb(240, 255, 220, 120) : Panel, b.X, b.Y, b.Width, b.Height, 12, on ? Accent : Color.FromArgb(110, 226, 168, 60));
                var col = !en ? Color.FromArgb(120, 200, 190, 170) : on ? Color.FromArgb(255, 60, 36, 12) : Cream;
                Txt(g, labels[i] + (i == 1 && !en ? "  (kosong)" : ""), fBtn, col, b.X + 24, b.Y + 11, on ? 0 : 2);
                if (on) g.DrawImage(Sprites.Hand, b.Right + 10 + MathF.Sin(anim * 6) * 4, b.Y + 11, 30, 30);
            }
            Txt(g, "Panah / W S pilih  |  Enter / klik  untuk memilih", fSmall, Color.FromArgb(230, 250, 238, 205), tx0 + 4, vh - 34, 1);

            if (showHelp) DrawHelp(g);
        }

        void DrawHelp(Graphics g)
        {
            using (var dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(dim, 0, 0, vw, vh);
            float w = Math.Min(620, vw - 40), h = 360, x = (vw - w) / 2, y = (vh - h) / 2;
            RRect(g, Color.FromArgb(245, 44, 32, 22), x, y, w, h, 14, Accent);
            Txt(g, "Cara Bermain", fBtn, Accent, x + 24, y + 16);
            string[] lines =
            {
                "WASD / Panah  -  bergerak (tahan Shift untuk lari)",
                "1-8 / roda mouse  -  ganti alat (cangkul, penyiram, kapak, beliung, bibit)",
                "Spasi / klik  -  pakai alat",
                "E  -  panen, isi air di kolam, buka toko, jual hasil di peti",
                "Z  -  tidur di rumah (tanaman yang disiram tumbuh, game tersimpan)",
                "P / Esc  -  jeda;  M saat jeda  -  simpan & ke menu utama",
                $"Tujuan: kumpulkan {FarmGame.GoalGold} emas."
            };
            for (int i = 0; i < lines.Length; i++) Txt(g, lines[i], fSmall, Color.White, x + 24, y + 64 + i * 32, 1);
            Txt(g, "Tekan Enter / Esc / klik untuk kembali", fSmall, Cream, x + 24, y + h - 34, 1);
        }

        public void Dispose()
        {
            scene?.Dispose();
            fTitle.Dispose(); fSub.Dispose(); fBtn.Dispose(); fSmall.Dispose(); fKicker.Dispose();
        }
    }
}
