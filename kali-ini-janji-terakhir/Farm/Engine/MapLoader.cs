using System.Drawing;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>Huruf khusus di peta yang menandai tempat munculnya objek (rumah, pohon, ayam, ...).</summary>
    public readonly record struct MapMarker(char Symbol, Point Tile);

    public sealed record LoadedMap(TileMap Map, IReadOnlyList<MapMarker> Markers);

    /// <summary>
    /// Membaca peta dari file teks (Assets/Maps/farm.txt). Satu huruf = satu tile:
    ///
    ///   .  rumput          =  jalan           d  tanah ladang (boleh dicangkul)
    ///   ~  air (padat)     *  rumput + bunga
    ///   T  pohon           B  pohon batas     o  batu        b  semak
    ///   f  pagar           H  rumah           S  toko        X  peti jual
    ///   P  posisi awal     c  ayam
    ///
    /// Huruf objek (T, B, o, ...) dikembalikan sebagai MapMarker; FarmWorld yang memutuskan
    /// objek apa yang dibuat untuk tiap huruf. Tanah di bawah objek mengikuti tetangganya:
    /// batu di tengah ladang berdiri di atas tanah ladang, pohon di padang berdiri di atas rumput.
    /// </summary>
    public static class MapLoader
    {
        public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Maps", "farm.txt");

        public static LoadedMap Load(string? path = null) => Parse(File.ReadAllLines(path ?? DefaultPath));

        public static LoadedMap Parse(IReadOnlyList<string> lines)
        {
            var rows = lines.Where(l => l.Length > 0).ToList();
            int width = rows.Max(r => r.Length), height = rows.Count;
            var map = new TileMap(width, height);
            var markers = new List<MapMarker>();

            for (int y = 0; y < height; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    var tile = new Point(x, y);
                    char c = rows[y][x];
                    switch (c)
                    {
                        case '.': break;
                        case '=': map[tile] = Terrain.Path; break;
                        case 'd': map[tile] = Terrain.Dirt; break;
                        case '~': map[tile] = Terrain.Water; break;
                        case '*': map.SetDecor(tile, $"decor_flower_{TileMap.Hash(tile) % 4}"); break;
                        default: markers.Add(new MapMarker(c, tile)); break;
                    }
                }

            foreach (var m in markers) map[m.Tile] = GroundUnder(map, m.Tile);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    AddRandomDecor(map, new Point(x, y), rows[y][x]);
            return new LoadedMap(map, markers);
        }

        /// <summary>Jenis tanah terbanyak di 4 tetangga (selain air); seri = rumput.</summary>
        private static Terrain GroundUnder(TileMap map, Point tile)
        {
            var counts = new Dictionary<Terrain, int>();
            foreach (var d in new[] { new Size(0, -1), new Size(1, 0), new Size(0, 1), new Size(-1, 0) })
            {
                var n = tile + d;
                if (!map.InBounds(n) || map[n] == Terrain.Water) continue;
                counts[map[n]] = counts.GetValueOrDefault(map[n]) + 1;
            }
            var best = counts.OrderByDescending(kv => kv.Value).FirstOrDefault();
            return best.Value >= 3 || (best.Value == 2 && counts.Count == 1) ? best.Key : Terrain.Grass;
        }

        /// <summary>Rumput liar dan teratai acak (tetap sama setiap kali dimuat) agar peta tidak polos.</summary>
        private static void AddRandomDecor(TileMap map, Point tile, char c)
        {
            int h = TileMap.Hash(tile);
            if (c == '.' && h % 100 < 9) map.SetDecor(tile, $"decor_tuft_{h % 2}");
            if (c == '~' && h % 100 < 12)
            {
                bool deepWater = TileMap.NeighborMask(tile, n => map[n] == Terrain.Water) == 15;
                if (deepWater) map.SetDecor(tile, $"decor_lily_{h % 2}");
            }
        }
    }
}
