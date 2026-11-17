using System.Drawing;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Farm.Ui
{
    /// <summary>Warna teks UI. Bentuk panel/bingkai berasal dari sprite (ui_frame, ui_slot, ...).</summary>
    public static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(70, 40, 20);          // teks di atas panel krem
        public static readonly Color InkSoft = Color.FromArgb(130, 90, 60);
        public static readonly Color Light = Color.FromArgb(252, 238, 205);     // teks di atas panel gelap
        public static readonly Color Gold = Color.FromArgb(250, 200, 60);
        public static readonly Color Good = Color.FromArgb(120, 210, 90);
        public static readonly Color Warn = Color.FromArgb(240, 110, 60);
        public static readonly Color Dim = Color.FromArgb(150, 0, 0, 0);
        public static readonly Color Selection = Color.FromArgb(70, 230, 140, 40);

        /// <summary>Penanda baris terpilih di menu: latar oranye tipis + panah di kiri.</summary>
        public static void Highlight(Renderer r, Rectangle row)
        {
            r.Tint(Selection, row);
            var arrow = Art.Get("ui_arrow_right");
            r.Draw(arrow, row.X - arrow.Width / 2f, row.Y + (row.Height - arrow.Height) / 2f);
        }
    }
}
