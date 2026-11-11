using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace kali_ini_janji_terakhir.Assets
{
    /// <summary>
    /// Utility for image operations: scaling, fitting, background cleanup, and fallback placeholders.
    /// </summary>
    public static class ImageProcessor
    {
        /// <summary>Scales so the longest side equals <paramref name="box"/>.</summary>
        public static Bitmap Fit(string path, int box)
        {
            var big = Scale(path, 0, 256);
            int w = big.Width >= big.Height ? box : 0, h = w == 0 ? box : 0;
            var r = Scale(big, w, h);
            big.Dispose();
            return r;
        }

        public static Bitmap Scale(string path, int w, int h, bool crop = true)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sprite file was not found: {path}", path);

            Bitmap src;
            try
            {
                using var raw = new Bitmap(path);
                src = crop ? Clean(raw) : new Bitmap(raw);
            }
            catch (ArgumentException ex)
            {
                // GDI+ reports malformed or unsupported image files only as
                // "Parameter is not valid". Keep the game usable and identify
                // the exact asset in the debug output.
                Debug.WriteLine($"Unable to load sprite '{path}': {ex.Message}");
                return CreatePlaceholder(w, h);
            }

            using (src)
                return Scale(src, w, h);
        }

        public static Bitmap Scale(Bitmap src, int w, int h)
        {
            if (w == 0) w = Math.Max(1, (int)Math.Round(src.Width * (h / (double)src.Height)));
            if (h == 0) h = Math.Max(1, (int)Math.Round(src.Height * (w / (double)src.Width)));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            return bmp;
        }

        /// <summary>
        /// Removes an opaque white background (flood fill from the border), drops
        /// near-invisible fringe pixels and crops the bitmap to its visible content.
        /// </summary>
        public static Bitmap Clean(Bitmap raw)
        {
            int W = raw.Width, H = raw.Height;
            var px = new int[W * H];
            using (var tmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(tmp)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(raw, 0, 0, W, H); }
                var bd = tmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(bd.Scan0, px, 0, px.Length);
                tmp.UnlockBits(bd);
            }

            static bool NearWhite(int c) => (c >>> 24) > 200 && ((c >> 16) & 255) > 235 && ((c >> 8) & 255) > 235 && (c & 255) > 235;

            if (NearWhite(px[0]))
            {
                var stack = new Stack<int>();
                for (int x = 0; x < W; x++) { stack.Push(x); stack.Push((H - 1) * W + x); }
                for (int y = 0; y < H; y++) { stack.Push(y * W); stack.Push(y * W + W - 1); }
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    if (!NearWhite(px[i])) continue;
                    px[i] = 0;
                    int x = i % W, y = i / W;
                    if (x > 0) stack.Push(i - 1);
                    if (x < W - 1) stack.Push(i + 1);
                    if (y > 0) stack.Push(i - W);
                    if (y < H - 1) stack.Push(i + W);
                }
            }

            int minX = W, minY = H, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if ((px[i] >>> 24) < 40) { px[i] = 0; continue; }
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            if (maxX < 0) { minX = minY = 0; maxX = W - 1; maxY = H - 1; }

            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            var res = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
            var rd = res.LockBits(new Rectangle(0, 0, cw, ch), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var row = new int[cw];
            for (int y = 0; y < ch; y++)
            {
                Array.Copy(px, (minY + y) * W + minX, row, 0, cw);
                Marshal.Copy(row, 0, rd.Scan0 + y * rd.Stride, cw);
            }
            res.UnlockBits(rd);
            return res;
        }

        public static Bitmap CreatePlaceholder(int w, int h)
        {
            if (w == 0) w = 150;
            if (h == 0) h = 150;

            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Magenta);
            using var pen = new Pen(Color.Black, Math.Max(1, Math.Min(w, h) / 16));
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            g.DrawLine(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            g.DrawLine(pen, bmp.Width - 1, 0, 0, bmp.Height - 1);
            return bmp;
        }
    }
}

