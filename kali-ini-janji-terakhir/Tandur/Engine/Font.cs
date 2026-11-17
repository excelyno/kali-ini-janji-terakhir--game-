using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Tandur.Engine
{
    /// <summary>Font bitmap yang di-bake dari PixelifySans ke atlas saat startup (tanpa anti-aliasing).</summary>
    public sealed class BitmapFont
    {
        public static readonly int[] Sizes = { 16, 24, 32, 56 };
        private static readonly string Charset = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + "éèàü";

        private readonly Atlas atlas;
        private readonly Dictionary<int, (Dictionary<char, float> Advance, int Height)> metrics;

        public BitmapFont(Atlas atlas, Dictionary<int, (Dictionary<char, float>, int)> metrics)
        {
            this.atlas = atlas;
            this.metrics = metrics;
        }

        /// <summary>Tambahkan gambar glyph ke AtlasBuilder. Mengembalikan metrik per ukuran.</summary>
        public static Dictionary<int, (Dictionary<char, float>, int)> Bake(AtlasBuilder builder, string fontFile)
        {
            var result = new Dictionary<int, (Dictionary<char, float>, int)>();
            using var pfc = new PrivateFontCollection();
            pfc.AddFontFile(fontFile);
            var family = pfc.Families[0];
            using var fmt = (StringFormat)StringFormat.GenericTypographic.Clone();
            fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            foreach (int size in Sizes)
            {
                using var font = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
                float em = family.GetEmHeight(FontStyle.Regular);
                int ascent = (int)Math.Ceiling(family.GetCellAscent(FontStyle.Regular) * size / em);
                int descent = (int)Math.Ceiling(family.GetCellDescent(FontStyle.Regular) * size / em);
                int height = ascent + descent + 2;
                var adv = new Dictionary<char, float>();
                using var probe = new Bitmap(4, 4);
                using var pg = Graphics.FromImage(probe);
                pg.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                foreach (char ch in Charset)
                {
                    float a = ch == ' ' ? pg.MeasureString("x x", font, 1000, fmt).Width - pg.MeasureString("xx", font, 1000, fmt).Width
                                        : pg.MeasureString(ch.ToString(), font, 1000, fmt).Width;
                    a = MathF.Round(a);
                    adv[ch] = a;
                    if (ch == ' ') continue;
                    var img = new Bitmap((int)a + 4, height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(img))
                    {
                        g.Clear(Color.Transparent);
                        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                        g.SmoothingMode = SmoothingMode.None;
                        g.DrawString(ch.ToString(), font, Brushes.White, 1, 0, fmt);
                    }
                    Harden(img);
                    if (ch == '5') SquareFive(img);
                    builder.Add($"font{size}:{ch}", img);
                }
                result[size] = (adv, height);
            }
            return result;
        }

        /// <summary>Pastikan semua piksel glyph benar-benar putih penuh atau transparan.</summary>
        private static void Harden(Bitmap img)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    img.SetPixel(x, y, img.GetPixel(x, y).A > 100 ? Color.White : Color.Transparent);
        }

        /// <summary>'5' Pixelify mirip huruf 'S'. Di game penuh angka (utang!) itu membingungkan,
        /// jadi angka 5 digambar ulang dari garis-garis persegi.</summary>
        private static void SquareFive(Bitmap m)
        {
            int x0 = m.Width, y0 = m.Height, x1 = 0, y1 = 0;
            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                    if (m.GetPixel(x, y).A > 0) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x + 1); y1 = Math.Max(y1, y + 1); }
            if (x1 <= x0) return;
            int Count(int y) { int c = 0; for (int x = x0; x < x1; x++) if (m.GetPixel(x, y).A > 0) c++; return c; }
            // Tebal garis horizontal = baris atas yang hampir selebar glyph. GDI kadang menyisakan
            // piksel tunggal di baris teratas, jadi abaikan baris yang terlalu sempit.
            int wide = Math.Max(1, (int)Math.Ceiling((x1 - x0) * 0.6));
            while (y0 < y1 && Count(y0) < wide) y0++;
            int th = 0;
            while (y0 + th < y1 && Count(y0 + th) >= wide) th++;
            th = Math.Clamp(th, 1, Math.Max(1, (y1 - y0) / 4));
            int tv = Math.Max(1, (int)Math.Round((x1 - x0) * 0.2));
            int ym = y0 + (y1 - y0 - th) / 2;
            for (int y = 0; y < m.Height; y++) for (int x = 0; x < m.Width; x++) m.SetPixel(x, y, Color.Transparent);
            void Fill(int ax, int ay, int bx, int by) { for (int y = ay; y <= by; y++) for (int x = ax; x <= bx; x++) m.SetPixel(x, y, Color.White); }
            Fill(x0, y0, x1 - 1, y0 + th - 1);
            Fill(x0, y0, x0 + tv - 1, ym + th - 1);
            Fill(x0, ym, x1 - 1, ym + th - 1);
            Fill(x1 - tv, ym, x1 - 1, y1 - 1);
            Fill(x0, y1 - th, x1 - 1, y1 - 1);
        }

        private int Snap(float size)
        {
            int best = Sizes[0];
            foreach (int s in Sizes) if (Math.Abs(s - size) < Math.Abs(best - size)) best = s;
            return best;
        }

        public int LineHeight(float size) => metrics[Snap(size)].Height;

        public float Measure(string text, float size = 24)
        {
            var adv = metrics[Snap(size)].Advance;
            float w = 0;
            foreach (char ch in text) w += adv.TryGetValue(ch, out var a) ? a : adv['?'];
            return w;
        }

        public List<string> Wrap(string text, float size, float maxWidth)
        {
            var lines = new List<string>();
            foreach (var para in text.Split('\n'))
            {
                string cur = "";
                foreach (var word in para.Split(' '))
                {
                    string trial = cur.Length == 0 ? word : cur + " " + word;
                    if (cur.Length > 0 && Measure(trial, size) > maxWidth) { lines.Add(cur); cur = word; }
                    else cur = trial;
                }
                lines.Add(cur);
            }
            return lines;
        }

        public float Draw(SpriteBatch batch, string text, float x, float y, float size, Rgba color)
        {
            int s = Snap(size);
            var adv = metrics[s].Advance;
            float cx = x;
            foreach (char c in text)
            {
                char ch = adv.ContainsKey(c) ? c : '?';
                if (ch != ' ') batch.Quad(atlas[$"font{s}:{ch}"], MathF.Round(cx - 1), MathF.Round(y), color: color);
                cx += adv[ch];
            }
            return cx;
        }
    }
}
