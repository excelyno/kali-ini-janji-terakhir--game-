using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        static readonly Color UiPanel = Color.FromArgb(215, 38, 28, 20), Accent = Color.FromArgb(255, 226, 168, 60), Cream = Color.FromArgb(255, 250, 238, 205);

        void RRect(Graphics g, Color fill, float x, float y, float w, float h, float r = 8, Color? border = null)
        {
            using var p = new GraphicsPath();
            float d = Math.Min(r * 2, Math.Min(w, h));
            p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure();
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
            if (border != null) { using var pen = new Pen(border.Value, 2); g.DrawPath(pen, p); }
        }

        void Txt(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using var sh = new SolidBrush(Color.FromArgb(170, 0, 0, 0)); using var b = new SolidBrush(c);
            g.DrawString(s, f, sh, x + 1, y + 1); g.DrawString(s, f, b, x, y);
        }

        void TxtC(Graphics g, string s, Font f, Color c, float cx, float y) { var sz = g.MeasureString(s, f); Txt(g, s, f, c, cx - sz.Width / 2, y); }

        void Bar(Graphics g, float x, float y, float w, float h, float frac, Color c)
        {
            frac = Math.Clamp(frac, 0, 1);
            RRect(g, Color.FromArgb(130, 0, 0, 0), x, y, w, h, h / 2);
            if (frac > 0.02f) RRect(g, c, x, y, Math.Max(h, w * frac), h, h / 2);
        }

        void DrawIcon(Graphics g, int i, float cx, float cy)
        {
            var img = i switch { 0 => Sprites.Hoe, 1 => Sprites.Can, 2 => Sprites.Axe, 3 => Sprites.Pick, _ => Sprites.SeedIcon };
            g.DrawImageUnscaled(img, (int)(cx - img.Width / 2f), (int)(cy - img.Height / 2f));
            if (i >= 4)
            {
                using var b = new SolidBrush(CropCol[i - 4]); using var pen = new Pen(Color.FromArgb(60, 40, 20), 1.5f);
                g.FillEllipse(b, cx + 4, cy + 6, 12, 12); g.DrawEllipse(pen, cx + 4, cy + 6, 12, 12);
            }
        }

        string Hint()
        {
            var foot = new Rectangle((int)px - 10, (int)py - 10, 20, 20);
            var tt = TargetTile();
            if (soil.TryGetValue(tt, out var s) && s.Crop >= 0 && s.Age >= Grow[s.Crop]) return "[E] Panen " + CropName[s.Crop];
            if (foot.IntersectsWith(shopZone)) return "[E] Buka Toko Bu Lastri";
            if (foot.IntersectsWith(binZone))
            {
                int total = wood * 10 + stone * 12; for (int i = 0; i < 4; i++) total += crops[i] * Sell[i];
                return total > 0 ? $"[E] Jual semua (+{total} emas)" : "[E] Peti jual (kosong)";
            }
            if (foot.IntersectsWith(pondZone)) return "[E] Isi air";
            if (foot.IntersectsWith(Rectangle.Inflate(houseRect, 30, 20))) return "[Z] Tidur sampai besok";
            return "";
        }

        void DrawNight(Graphics g)
        {
            if (tm > 960)
            {
                float t = Math.Min(1f, (tm - 960) / 480f), c = Math.Min(1f, t * 2f);
                var dusk = Color.FromArgb((int)(255 + (10 - 255) * c), (int)(120 + (10 - 120) * c), (int)(40 + (60 - 40) * c));
                using var b = new SolidBrush(Color.FromArgb((int)(t * 150), dusk)); g.FillRectangle(b, 0, 0, vw, vh);
            }
            else if (tm < 420)
            {
                using var b = new SolidBrush(Color.FromArgb((int)((420 - tm) / 60f * 90), 10, 10, 60)); g.FillRectangle(b, 0, 0, vw, vh);
            }
        }

        void DrawHud(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            RRect(g, UiPanel, 10, 10, 270, 128, 12, Color.FromArgb(120, 226, 168, 60));
            int h = (int)(tm / 60), m = (int)(tm % 60) / 10 * 10;
            Txt(g, $"Hari {day}", fMed, Cream, 22, 16);
            Txt(g, $"{h:00}:{m:00}", fMed, Cream, 214, 16);
            Txt(g, $"Emas {gold}", fMed, Accent, 22, 40);
            Txt(g, "/ " + GoalGold, fSmall, Color.Silver, 214, 45);
            Bar(g, 22, 68, 246, 8, gold / (float)GoalGold, Accent);
            Txt(g, "Energi", fSmall, Color.White, 22, 80);
            Bar(g, 80, 85, 188, 10, energy / 100f, Color.LimeGreen);
            Txt(g, "Air", fSmall, Color.White, 22, 102);
            Bar(g, 80, 107, 188, 10, water / waterCap, Color.DeepSkyBlue);

            int sw = 64, x0 = vw / 2 - sw * 4, y0 = vh - sw - 30;
            RRect(g, UiPanel, x0 - 8, y0 - 8, sw * 8 + 12, sw + 12, 12);
            for (int i = 0; i < 8; i++)
            {
                int x = x0 + i * sw;
                bool sel = i == slot;
                RRect(g, sel ? Color.FromArgb(235, 255, 220, 120) : Color.FromArgb(150, 70, 55, 40), x, y0, sw - 4, sw - 4, 8, sel ? Accent : null);
                DrawIcon(g, i, x + (sw - 4) / 2f, y0 + 26);
                var dark = Color.FromArgb(50, 30, 10);
                Txt(g, SlotName[i], fSmall, sel ? dark : Cream, x + 3, y0 + 44);
                Txt(g, (i + 1).ToString(), fSmall, sel ? dark : Color.Silver, x + 4, y0 + 2);
                if (i >= 4) Txt(g, "x" + seeds[i - 4], fSmall, sel ? dark : Color.White, x + 36, y0 + 2);
            }
            string inv = $"Kayu {wood}   Batu {stone}   " + string.Join("   ", Enumerable.Range(0, 4).Select(i => CropName[i] + " " + crops[i]));
            var isz = g.MeasureString(inv, fSmall);
            RRect(g, UiPanel, vw / 2 - isz.Width / 2 - 10, y0 - 40, isz.Width + 20, 26, 8);
            Txt(g, inv, fSmall, Cream, vw / 2 - isz.Width / 2, y0 - 36);
            g.DrawImageUnscaled(Sprites.Backpack, (int)(vw / 2 - isz.Width / 2 - 52), y0 - 46);

            string hint = Hint();
            if (hint != "")
            {
                var hs = g.MeasureString(hint, fMed);
                float hw = hs.Width + 24 + 30;
                RRect(g, Color.FromArgb(220, 30, 60, 30), vw / 2 - hw / 2, y0 - 82, hw, 36, 10, Color.LimeGreen);
                g.DrawImageUnscaled(Sprites.Hand, (int)(vw / 2 - hw / 2 + 8), y0 - 77);
                Txt(g, hint, fMed, Color.White, vw / 2 - hw / 2 + 42, y0 - 77);
            }

            Txt(g, "WASD gerak | Shift lari | Spasi/klik alat | E aksi | 1-8 alat | Z tidur | P jeda", fSmall, Color.White, 14, vh - 22);
            if (msgT > 0)
            {
                var sz = g.MeasureString(msg, fMed);
                RRect(g, Color.FromArgb(215, 0, 0, 0), vw / 2 - sz.Width / 2 - 12, 18, sz.Width + 24, sz.Height + 8, 10);
                Txt(g, msg, fMed, Color.White, vw / 2 - sz.Width / 2, 22);
            }
        }

        void DrawShop(Graphics g)
        {
            int w = 460, h = 400, x = vw / 2 - w / 2, y = vh / 2 - h / 2;
            RRect(g, Color.FromArgb(240, 44, 32, 22), x, y, w, h, 14, Accent);
            Txt(g, "Toko Bu Lastri", fMed, Accent, x + 18, y + 12);
            Txt(g, "Emas: " + gold, fMed, Color.White, x + w - 130, y + 12);
            string[] names = { "Bibit Wortel", "Bibit Tomat", "Bibit Jagung", "Bibit Labu", "Pulihkan energi", "Kaleng besar", "", "Tutup" };
            string[] price = { "20", "35", "50", "90", "30", "300", "", "" };
            for (int i = 0; i < 8; i++)
            {
                if (i == 6) continue;
                int yy = y + 52 + i * 38;
                if (i == shopSel) RRect(g, Color.FromArgb(120, 255, 220, 100), x + 10, yy - 2, w - 20, 34, 8);
                bool owned = i == 5 && waterCap > 12;
                Txt(g, names[i] + (i < 4 ? $"  (punya {seeds[i]})" : ""), fMed, Color.White, x + 22, yy + 4);
                if (i < 6) Txt(g, owned ? "Dimiliki" : price[i] + " emas", fMed, owned ? Color.Silver : Accent, x + w - 130, yy + 4);
            }
            Txt(g, "Atas/Bawah pilih | Enter beli | Esc tutup", fSmall, Color.Silver, x + 18, y + h - 28);
        }

        void Banner(Graphics g, string a, string b)
        {
            Rect(g, Color.FromArgb(200, 0, 0, 0), 0, vh / 2 - 80, vw, 160);
            TxtC(g, a, fBig, Accent, vw / 2f, vh / 2 - 60);
            TxtC(g, b, fMed, Color.White, vw / 2f, vh / 2 + 4);
            TxtC(g, "Tekan R untuk main lagi  |  Esc / M ke menu utama", fMed, Cream, vw / 2f, vh / 2 + 36);
        }

        void DrawPause(Graphics g)
        {
            Rect(g, Color.FromArgb(150, 0, 0, 0), 0, 0, vw, vh);
            TxtC(g, "Jeda", fBig, Accent, vw / 2f, vh / 2f - 40);
            TxtC(g, "Tekan P atau Esc untuk lanjut", fMed, Color.White, vw / 2f, vh / 2f + 10);
            TxtC(g, "Tekan M untuk simpan & kembali ke menu utama", fMed, Cream, vw / 2f, vh / 2f + 40);
        }
    }
}
