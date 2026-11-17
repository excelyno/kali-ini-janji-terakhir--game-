using System.Drawing;
using System.Drawing.Imaging;

namespace Tandur.Engine
{
    /// <summary>
    /// Membangkitkan sprite tambahan dari sprite TANDUR saat atlas dibangun, agar gerak terasa hidup:
    ///   * {nama}_{arah}_run_0..3  : frame lari (badan condong ke depan + memantul), dipakai saat Shift.
    ///   * {nama}_{arah}_act_0..2  : ancang-ancang, hentakan, dan kembali saat mengayun alat.
    ///   * {nama}_{arah}_idle_2    : frame kedip/napas untuk variasi diam.
    ///   * dust_0..3, sparkle_0..3, ripple_0..3, leaf_0..1 : efek kecil (debu langkah, kilau air, riak).
    /// Bayangan (piksel semi-transparan di kaki) tidak ikut digeser, jadi kaki tetap "menempel" ke tanah.
    /// </summary>
    public static class SpriteForge
    {
        private static readonly string[] Dirs = { "down", "side", "up" };

        public static void Forge(AtlasBuilder b)
        {
            var people = b.Images.Keys.Where(k => k.EndsWith("_down_walk_0")).Select(k => k[..^"_down_walk_0".Length]).ToList();
            foreach (var who in people)
                foreach (var dir in Dirs)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        string src = $"{who}_{dir}_walk_{i}";
                        if (!b.Has(src)) continue;
                        int bob = i % 2 == 1 ? -1 : 0;
                        int lean = dir == "side" ? 1 : 0;
                        AddDerived(b, src, $"{who}_{dir}_run_{i}", Shift(b.Images[src], lean, bob, leanTopOnly: true));
                    }
                    string idle = $"{who}_{dir}_idle_0";
                    if (!b.Has(idle)) continue;
                    var baseImg = b.Images[idle];
                    AddDerived(b, idle, $"{who}_{dir}_act_0", Shift(baseImg, dir == "side" ? -1 : 0, -1, leanTopOnly: true));
                    AddDerived(b, idle, $"{who}_{dir}_act_1", Squash(baseImg));
                    AddDerived(b, idle, $"{who}_{dir}_act_2", Shift(baseImg, 0, 0, leanTopOnly: false));
                    AddDerived(b, idle, $"{who}_{dir}_idle_2", dir == "up" ? Shift(baseImg, 0, 1, leanTopOnly: true) : Blink(baseImg));
                }

            for (int i = 0; i < 4; i++)
            {
                b.Add($"dust_{i}", Dust(i), (4, 6));
                b.Add($"sparkle_{i}", Sparkle(i), (3, 3));
                b.Add($"ripple_{i}", Ripple(i), (8, 4));
            }
            for (int i = 0; i < 2; i++) b.Add($"leaf_{i}", Leaf(i), (2, 2));
        }

        private static void AddDerived(AtlasBuilder b, string src, string name, Bitmap img) => b.Add(name, img, b.AnchorOf(src));

        private static Color[,] Pixels(Bitmap img)
        {
            var p = new Color[img.Width, img.Height];
            for (int y = 0; y < img.Height; y++) for (int x = 0; x < img.Width; x++) p[x, y] = img.GetPixel(x, y);
            return p;
        }

        private static bool Body(Color c) => c.A == 255;

        /// <summary>Geser piksel badan (bukan bayangan). leanTopOnly: hanya separuh atas yang condong.</summary>
        private static Bitmap Shift(Bitmap src, int dx, int dy, bool leanTopOnly)
        {
            var p = Pixels(src);
            int w = src.Width, h = src.Height;
            var outImg = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            int top = int.MaxValue, bottom = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Body(p[x, y])) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            int mid = top + (bottom - top) / 2;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (!Body(p[x, y]) && p[x, y].A > 0) outImg.SetPixel(x, y, p[x, y]);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Body(p[x, y])) continue;
                    int sx = leanTopOnly && y > mid ? 0 : dx;
                    int nx = x + sx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h) outImg.SetPixel(nx, ny, p[x, y]);
                }
            return outImg;
        }

        /// <summary>Saat alat menghantam tanah: badan memendek 1 piksel (squash) dan sedikit melebar.</summary>
        private static Bitmap Squash(Bitmap src)
        {
            var p = Pixels(src);
            int w = src.Width, h = src.Height;
            var o = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            int top = int.MaxValue;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Body(p[x, y])) top = Math.Min(top, y);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (!Body(p[x, y]) && p[x, y].A > 0) o.SetPixel(x, y, p[x, y]);
            int skip = top + 3;                     // buang satu baris di bawah kepala: badan merunduk
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    if (!Body(p[x, y]) || y == skip) continue;
                    int ny = y < skip ? y + 1 : y;
                    o.SetPixel(x, ny, p[x, y]);
                }
            return o;
        }

        /// <summary>Kedip: baris tergelap di area wajah (mata) diganti warna kulit di atasnya.</summary>
        private static Bitmap Blink(Bitmap src)
        {
            var o = new Bitmap(src);
            int w = src.Width, top = -1;
            for (int y = 0; y < src.Height && top < 0; y++) for (int x = 0; x < w; x++) if (src.GetPixel(x, y).A == 255) { top = y; break; }
            if (top < 0) return o;
            int bestRow = -1; float bestDark = 0;
            for (int y = top + 3; y < Math.Min(src.Height, top + 11); y++)
            {
                int dark = 0, skin = 0;
                for (int x = 0; x < w; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A < 255) continue;
                    if (c.GetBrightness() < 0.22f) dark++; else skin++;
                }
                if (dark >= 2 && dark <= 6 && skin >= 4 && dark > bestDark) { bestDark = dark; bestRow = y; }
            }
            if (bestRow < 0) return o;
            for (int x = 0; x < w; x++)
            {
                var c = src.GetPixel(x, bestRow);
                if (c.A == 255 && c.GetBrightness() < 0.22f)
                {
                    var above = src.GetPixel(x, bestRow - 1);
                    o.SetPixel(x, bestRow, above.A == 255 && above.GetBrightness() >= 0.22f ? above : Color.FromArgb(255, c.R + 40 > 255 ? 255 : c.R + 40, Math.Min(255, c.G + 30), Math.Min(255, c.B + 20)));
                }
            }
            return o;
        }

        private static Bitmap Dust(int i)
        {
            var o = new Bitmap(9, 7, PixelFormat.Format32bppArgb);
            int r = 1 + i;
            var c = Color.FromArgb(Math.Max(60, 220 - i * 50), 222, 205, 170);
            var cDark = Color.FromArgb(Math.Max(40, 170 - i * 40), 180, 160, 125);
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 9; x++)
                {
                    float dx = (x - 4) / (float)(r + 0.5f), dy = (y - 4) / (float)(r * 0.6f + 0.5f);
                    float d = dx * dx + dy * dy;
                    if (d <= 1f) o.SetPixel(x, y, y > 4 ? cDark : c);
                }
            return o;
        }

        private static Bitmap Sparkle(int i)
        {
            var o = new Bitmap(7, 7, PixelFormat.Format32bppArgb);
            int len = new[] { 1, 2, 3, 1 }[i];
            var core = Color.FromArgb(255, 255, 255, 240);
            var arm = Color.FromArgb(200, 255, 244, 190);
            o.SetPixel(3, 3, core);
            for (int k = 1; k <= len; k++)
            {
                o.SetPixel(3 + k, 3, arm); o.SetPixel(3 - k, 3, arm);
                o.SetPixel(3, 3 + k, arm); o.SetPixel(3, 3 - k, arm);
            }
            return o;
        }

        private static Bitmap Ripple(int i)
        {
            var o = new Bitmap(17, 9, PixelFormat.Format32bppArgb);
            float rx = 3 + i * 1.6f, ry = rx * 0.5f;
            var c = Color.FromArgb(Math.Max(50, 200 - i * 45), 220, 240, 255);
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 17; x++)
                {
                    float d = MathF.Sqrt(MathF.Pow((x - 8) / rx, 2) + MathF.Pow((y - 4) / ry, 2));
                    if (MathF.Abs(d - 1f) < 0.22f) o.SetPixel(x, y, c);
                }
            return o;
        }

        private static Bitmap Leaf(int i)
        {
            var o = new Bitmap(5, 4, PixelFormat.Format32bppArgb);
            var c = i == 0 ? Color.FromArgb(255, 120, 178, 84) : Color.FromArgb(255, 96, 150, 70);
            foreach (var (x, y) in new[] { (1, 1), (2, 1), (3, 1), (2, 0), (2, 2), (1, 2) }) o.SetPixel(x, y, c);
            return o;
        }
    }
}
