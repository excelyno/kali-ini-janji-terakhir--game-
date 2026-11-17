using System.Drawing;
using System.Drawing.Imaging;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    public enum Terrain { Grass, Path, Dirt, Water }

    /// <summary>
    /// Peta grid berukuran Width x Height tile. Menyimpan jenis tanah tiap tile dan hiasan kecil
    /// (bunga, rumput). Tile air bersifat padat (tidak bisa diinjak).
    ///
    /// Lapisan tanah digambar SEKALI ke sebuah bitmap besar (RenderGround) lalu tiap frame
    /// cukup disalin bagian yang terlihat kamera. Inilah cara murah menggambar ribuan tile di GDI+.
    /// </summary>
    public sealed class TileMap
    {
        public const int Tile = Core.GameConfig.Tile;

        // Bit autotile: tetangga mana yang jenisnya SAMA.
        public const int North = 1, East = 2, South = 4, West = 8;

        private readonly Terrain[,] terrain;
        private readonly string?[,] decor;

        public TileMap(int width, int height)
        {
            Width = width;
            Height = height;
            terrain = new Terrain[width, height];
            decor = new string?[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public int PixelWidth => Width * Tile;
        public int PixelHeight => Height * Tile;
        public SizeF PixelSize => new(PixelWidth, PixelHeight);

        public bool InBounds(Point t) => t.X >= 0 && t.Y >= 0 && t.X < Width && t.Y < Height;

        public Terrain this[Point t]
        {
            get => InBounds(t) ? terrain[t.X, t.Y] : Terrain.Grass;
            set { if (InBounds(t)) terrain[t.X, t.Y] = value; }
        }

        public void SetDecor(Point t, string? sprite)
        {
            if (InBounds(t)) decor[t.X, t.Y] = sprite;
        }

        /// <summary>Tile yang tidak bisa dilewati: di luar peta atau air.</summary>
        public bool IsSolid(Point t) => !InBounds(t) || terrain[t.X, t.Y] == Terrain.Water;

        // ------------------------------------------------------------------ konversi koordinat
        public static Point TileAt(float x, float y) => new((int)MathF.Floor(x / Tile), (int)MathF.Floor(y / Tile));
        public static Rectangle TileRect(Point t) => new(t.X * Tile, t.Y * Tile, Tile, Tile);
        public static PointF TileCenter(Point t) => new(t.X * Tile + Tile / 2f, t.Y * Tile + Tile / 2f);

        /// <summary>
        /// AUTOTILE: hitung mask 4-bit dari tetangga yang "sama" (N=1, E=2, S=4, W=8).
        /// Sprite "{jenis}_{mask}" sudah disiapkan generator untuk ke-16 kemungkinan.
        /// </summary>
        public static int NeighborMask(Point t, Func<Point, bool> same)
        {
            int mask = 0;
            if (same(new Point(t.X, t.Y - 1))) mask |= North;
            if (same(new Point(t.X + 1, t.Y))) mask |= East;
            if (same(new Point(t.X, t.Y + 1))) mask |= South;
            if (same(new Point(t.X - 1, t.Y))) mask |= West;
            return mask;
        }

        // ------------------------------------------------------------------ gambar
        /// <summary>Gambar seluruh lapisan tanah (rumput + jalan/ladang/air + hiasan) ke satu bitmap.</summary>
        public Bitmap RenderGround()
        {
            var bmp = new Bitmap(PixelWidth, PixelHeight, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            var r = new Renderer(g);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var t = new Point(x, y);
                    int px = x * Tile, py = y * Tile;
                    r.Draw($"grass_{Hash(t) % 4}", px, py);

                    var kind = terrain[x, y];
                    if (kind != Terrain.Grass)
                    {
                        int mask = NeighborMask(t, n => !InBounds(n) || this[n] == kind);
                        r.Draw($"{TerrainSprite(kind)}_{mask}", px, py);
                    }
                    if (decor[x, y] is { } d)
                    {
                        var s = Art.Get(d);
                        r.Draw(s, px + 8 + Hash(t) % 24, py + 10 + Hash(t) / 7 % 20);
                    }
                }
            return bmp;
        }

        private static string TerrainSprite(Terrain t) => t switch
        {
            Terrain.Path => "path",
            Terrain.Dirt => "dirt",
            Terrain.Water => "water",
            _ => "grass",
        };

        /// <summary>Bilangan acak tetap per tile (selalu sama untuk tile yang sama).</summary>
        public static int Hash(Point t)
        {
            unchecked
            {
                uint h = (uint)(t.X * 73856093) ^ (uint)(t.Y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return (int)(h >> 8 & 0x7FFF);
            }
        }
    }
}
