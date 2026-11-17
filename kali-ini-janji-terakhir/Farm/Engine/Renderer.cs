using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>
    /// Pembungkus Graphics. Kode game hanya memakai method di sini (gambar sprite, 9-slice, teks),
    /// tidak pernah menggambar bentuk manual seperti FillEllipse atau DrawLine.
    /// Semua sprite digambar di pivot-nya dengan nearest-neighbour agar pixel art tetap tajam.
    /// </summary>
    public sealed class Renderer
    {
        private static readonly ImageAttributes NoBleed = CreateNoBleed();
        private static readonly ImageAttributes?[] AlphaCache = new ImageAttributes?[17];

        public Renderer(Graphics g)
        {
            G = g;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        public Graphics G { get; }

        // ------------------------------------------------------------------ kamera
        /// <summary>Mulai menggambar dalam koordinat dunia (digeser oleh kamera).</summary>
        public void BeginWorld(Camera2D camera)
        {
            G.ResetTransform();
            G.TranslateTransform(-(int)camera.X, -(int)camera.Y);
        }

        /// <summary>Kembali ke koordinat layar (untuk HUD dan jendela).</summary>
        public void BeginScreen() => G.ResetTransform();

        // ------------------------------------------------------------------ sprite
        public void Draw(Sprite s, float x, float y)
            => G.DrawImage(s.Image, (int)MathF.Round(x) - s.Pivot.X, (int)MathF.Round(y) - s.Pivot.Y, s.Width, s.Height);

        public void Draw(string sprite, float x, float y) => Draw(Art.Get(sprite), x, y);

        /// <summary>Gambar semi-transparan (memudar). alpha 0..1, dibulatkan ke 1/16 agar bisa di-cache.</summary>
        public void Draw(Sprite s, float x, float y, float alpha)
        {
            int level = (int)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 16);
            if (level >= 16) { Draw(s, x, y); return; }
            if (level <= 0) return;
            var dest = new Rectangle((int)MathF.Round(x) - s.Pivot.X, (int)MathF.Round(y) - s.Pivot.Y, s.Width, s.Height);
            G.DrawImage(s.Image, dest, 0, 0, s.Width, s.Height, GraphicsUnit.Pixel, AlphaAttributes(level));
        }

        /// <summary>Gambar dengan titik tengah sprite di (cx, cy), mengabaikan pivot. Untuk ikon.</summary>
        public void DrawCentered(Sprite s, float cx, float cy)
            => G.DrawImage(s.Image, (int)(cx - s.Width / 2f), (int)(cy - s.Height / 2f), s.Width, s.Height);

        /// <summary>Gambar sprite dengan ukuran berbeda (misal potret di dialog, isi bar).</summary>
        public void DrawStretched(Sprite s, RectangleF dest)
        {
            if (dest.Width <= 0 || dest.Height <= 0) return;
            var d = Rectangle.Round(dest);
            G.DrawImage(s.Image, d, 0, 0, s.Width, s.Height, GraphicsUnit.Pixel, NoBleed);
        }

        /// <summary>
        /// Bingkai 9-slice: pojok tetap, sisi melar satu arah, tengah melar dua arah.
        /// Ukuran pojok diambil dari pivot sprite (dicatat oleh generator).
        /// </summary>
        public void DrawFrame(Sprite s, Rectangle dest)
        {
            int c = Math.Max(1, s.Pivot.X);
            int w = s.Width, h = s.Height;
            int[] sx = { 0, c, w - c, w }, sy = { 0, c, h - c, h };
            int[] dx = { dest.Left, dest.Left + c, dest.Right - c, dest.Right };
            int[] dy = { dest.Top, dest.Top + c, dest.Bottom - c, dest.Bottom };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    var d = Rectangle.FromLTRB(dx[col], dy[row], dx[col + 1], dy[row + 1]);
                    if (d.Width <= 0 || d.Height <= 0) continue;
                    G.DrawImage(s.Image, d, sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row], GraphicsUnit.Pixel, NoBleed);
                }
        }

        public void DrawFrame(string sprite, int x, int y, int w, int h) => DrawFrame(Art.Get(sprite), new Rectangle(x, y, w, h));

        /// <summary>Bar isi (energi, air, misi): latar 9-slice + isi yang direntang sesuai persentase.</summary>
        public void DrawBar(Rectangle dest, float fraction, string fill, bool vertical = false)
        {
            DrawFrame(Art.Get("ui_bar_back"), dest);
            var inner = Rectangle.Inflate(dest, -4, -4);
            fraction = Math.Clamp(fraction, 0f, 1f);
            var filled = vertical
                ? new RectangleF(inner.X, inner.Bottom - inner.Height * fraction, inner.Width, inner.Height * fraction)
                : new RectangleF(inner.X, inner.Y, inner.Width * fraction, inner.Height);
            DrawStretched(Art.Get(fill), filled);
        }

        // ------------------------------------------------------------------ teks
        public SizeF Measure(string text, Font font) => G.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);

        public void Text(string text, Font font, Color color, float x, float y, bool shadow = true, float alpha = 1f)
        {
            int a = (int)(255 * Math.Clamp(alpha, 0f, 1f));
            if (shadow)
            {
                using var sh = new SolidBrush(Color.FromArgb(a * 150 / 255, 40, 22, 10));
                G.DrawString(text, font, sh, x + 2, y + 2, StringFormat.GenericTypographic);
            }
            using var b = new SolidBrush(Color.FromArgb(a, color));
            G.DrawString(text, font, b, x, y, StringFormat.GenericTypographic);
        }

        public void TextCentered(string text, Font font, Color color, float cx, float y, bool shadow = true)
            => Text(text, font, color, cx - Measure(text, font).Width / 2, y, shadow);

        /// <summary>Pecah teks agar muat dalam lebar tertentu.</summary>
        public List<string> Wrap(string text, Font font, float width)
        {
            var lines = new List<string>();
            string line = "";
            foreach (var word in text.Split(' '))
            {
                string test = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(test, font).Width > width) { lines.Add(line); line = word; }
                else line = test;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        // ------------------------------------------------------------------ lapisan warna
        /// <summary>Satu lapisan warna transparan di atas layar (malam, mendung, layar redup).</summary>
        public void Tint(Color color, Rectangle area)
        {
            if (color.A == 0) return;
            using var b = new SolidBrush(color);
            G.FillRectangle(b, area);
        }

        private static ImageAttributes CreateNoBleed()
        {
            var a = new ImageAttributes();
            a.SetWrapMode(WrapMode.TileFlipXY);   // cegah garis pinggir saat sprite direntang
            return a;
        }

        private static ImageAttributes AlphaAttributes(int level)
        {
            if (AlphaCache[level] is { } cached) return cached;
            var a = new ImageAttributes();
            a.SetColorMatrix(new ColorMatrix { Matrix33 = level / 16f });
            a.SetWrapMode(WrapMode.TileFlipXY);
            return AlphaCache[level] = a;
        }
    }
}
