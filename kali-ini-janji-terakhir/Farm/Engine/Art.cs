using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>
    /// Satu gambar siap pakai beserta pivot-nya (titik jangkar).
    /// Untuk benda di dunia pivot = titik kaki/alas, jadi sprite cukup digambar di posisi benda.
    /// </summary>
    public sealed class Sprite
    {
        public Sprite(string name, Bitmap image, Point pivot)
        {
            Name = name;
            Image = image;
            Pivot = pivot;
            Width = image.Width;     // disimpan sekali: Bitmap.Width memanggil GDI+ setiap kali dibaca
            Height = image.Height;
        }

        public string Name { get; }
        public Bitmap Image { get; }
        public Point Pivot { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Kotak gambar di dunia jika pivot diletakkan di (x, y).</summary>
        public RectangleF BoundsAt(float x, float y) => new(x - Pivot.X, y - Pivot.Y, Width, Height);
    }

    /// <summary>
    /// Gudang sprite. Semua PNG di Assets/Sprites dimuat sekali saat permainan mulai.
    /// File-file itu dibuat oleh AssetGen/generate.py dari aset lama di Assets/FileAssets,
    /// sudah dalam ukuran layar, jadi tidak ada skala/pemotongan saat game berjalan.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite[]> frameCache = new(StringComparer.Ordinal);
        private static readonly HashSet<string> reportedMissing = new(StringComparer.Ordinal);
        private static readonly object gate = new();   // aman dipanggil dari beberapa thread (misalnya unit test paralel)
        private static bool loaded;

        public static string Folder => Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites");

        public static void Load()
        {
            lock (gate)
            {
                if (loaded) return;
                if (Directory.Exists(Folder))
                {
                    var pivots = ReadPivots(Path.Combine(Folder, "sprites.json"));
                    foreach (string path in Directory.GetFiles(Folder, "*.png"))
                    {
                        string name = Path.GetFileNameWithoutExtension(path);
                        pivots.TryGetValue(name, out var pivot);
                        sprites[name] = new Sprite(name, LoadFast(path), pivot);
                    }
                }
                loaded = true;   // baru ditandai selesai setelah semua sprite masuk
            }
        }

        public static bool Has(string name)
        {
            Load();
            lock (gate) return sprites.ContainsKey(name);
        }

        /// <summary>Ambil sprite berdasarkan nama. Nama yang salah menghasilkan kotak magenta (tidak crash).</summary>
        public static Sprite Get(string name)
        {
            Load();
            lock (gate)
            {
                if (sprites.TryGetValue(name, out var sprite)) return sprite;
                if (reportedMissing.Add(name)) System.Diagnostics.Debug.WriteLine("Sprite tidak ditemukan: " + name);
                return sprites[name] = Placeholder(name);
            }
        }

        /// <summary>Frame animasi berurutan: "{prefix}_0", "{prefix}_1", ...</summary>
        public static Sprite[] Frames(string prefix)
        {
            Load();
            lock (gate)
            {
                if (frameCache.TryGetValue(prefix, out var cached)) return cached;
                var list = new List<Sprite>();
                while (sprites.TryGetValue($"{prefix}_{list.Count}", out var s)) list.Add(s);
                if (list.Count == 0) list.Add(Get(prefix));
                return frameCache[prefix] = list.ToArray();
            }
        }

        private static Dictionary<string, Point> ReadPivots(string manifest)
        {
            var result = new Dictionary<string, Point>(StringComparer.Ordinal);
            if (!File.Exists(manifest)) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            foreach (var p in doc.RootElement.GetProperty("pivots").EnumerateObject())
                result[p.Name] = new Point(p.Value[0].GetInt32(), p.Value[1].GetInt32());
            return result;
        }

        /// <summary>Format PArgb adalah format tercepat untuk digambar oleh GDI+.</summary>
        private static Bitmap LoadFast(string path)
        {
            using var file = new Bitmap(path);
            var bmp = new Bitmap(file.Width, file.Height, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(file, new Rectangle(0, 0, file.Width, file.Height));
            return bmp;
        }

        private static Sprite Placeholder(string name)
        {
            var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Magenta);
            return new Sprite(name, bmp, new Point(8, 16));
        }
    }
}
