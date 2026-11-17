using System.Drawing;
using System.Drawing.Text;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>
    /// Font piksel (Pixelify Sans, lisensi OFL di Assets/Fonts) agar teks serasi dengan pixel art.
    /// Jika file font tidak ada, dipakai Segoe UI.
    /// </summary>
    public static class Fonts
    {
        private static readonly PrivateFontCollection collection = new();
        private static readonly FontFamily family = LoadFamily();

        public static readonly Font Small = Make(11f);
        public static readonly Font Body = Make(14f);
        public static readonly Font Large = Make(18f);
        public static readonly Font Title = Make(40f);

        public static Font Make(float size) => new(family, size, FontStyle.Regular, GraphicsUnit.Pixel);

        private static FontFamily LoadFamily()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "PixelifySans.ttf");
            try
            {
                if (File.Exists(path))
                {
                    collection.AddFontFile(path);
                    return collection.Families[0];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Font gagal dimuat: " + ex.Message);
            }
            return new FontFamily("Segoe UI");
        }
    }
}
