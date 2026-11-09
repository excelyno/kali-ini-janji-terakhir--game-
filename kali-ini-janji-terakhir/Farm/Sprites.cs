using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace kali_ini_janji_terakhir.Farm
{
    public static class Sprites
    {
        public static Bitmap Tree = null!, Rock = null!, Fence = null!, Fence2 = null!, Shop = null!, Crates = null!;

        public static void Load()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "FileAssets");
            Tree = Scale(Path.Combine(dir, "pohon.png"), 0, 150);
            Rock = Scale(Path.Combine(dir, "batu.png"), 64, 0);
            Fence = Scale(Path.Combine(dir, "pagar.png"), 0, 40);
            Fence2 = Scale(Path.Combine(dir, "pagar-2.png"), 0, 40);
            Shop = Scale(Path.Combine(dir, "toko-bu-lastri.png"), 288, 0);
            Crates = Scale(Path.Combine(dir, "peti-barang.png"), 0, 84);
        }

        private static Bitmap Scale(string path, int w, int h)
        {
            using var src = new Bitmap(path);
            if (w == 0) w = Math.Max(1, (int)Math.Round(src.Width * (h / (double)src.Height)));
            if (h == 0) h = Math.Max(1, (int)Math.Round(src.Height * (w / (double)src.Width)));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, w, h);
            return bmp;
        }
    }
}
