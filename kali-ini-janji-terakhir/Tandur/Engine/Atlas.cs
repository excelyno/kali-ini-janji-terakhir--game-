using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using OpenTK.Graphics.OpenGL4;
using GLPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace Tandur.Engine
{
    /// <summary>Satu gambar di dalam atlas. Immutable (ENKAPSULASI: tidak bisa diubah dari luar).</summary>
    public sealed record Region(string Name, int X, int Y, int W, int H, float U0, float V0, float U1, float V1, int Ax = 0, int Ay = 0)
    {
        /// <summary>Potongan region dalam piksel seni relatif ke pojok kiri-atas (untuk 9-slice).</summary>
        public Region Sub(int sx, int sy, int sw, int sh)
        {
            float du = (U1 - U0) / W, dv = (V1 - V0) / H;
            return this with { W = sw, H = sh, U0 = U0 + sx * du, V0 = V0 + sy * dv, U1 = U0 + (sx + sw) * du, V1 = V0 + (sy + sh) * dv };
        }
    }

    /// <summary>Mengumpulkan semua sprite PNG + glyph font, lalu mem-pack menjadi satu tekstur.</summary>
    public sealed class AtlasBuilder
    {
        private readonly int pad;
        private readonly Dictionary<string, Bitmap> images = new();
        private readonly Dictionary<string, (int, int)> anchors = new();

        public AtlasBuilder(int pad = 1) => this.pad = pad;

        public IReadOnlyDictionary<string, Bitmap> Images => images;

        public bool Has(string name) => images.ContainsKey(name);
        public (int X, int Y) AnchorOf(string name) => anchors.TryGetValue(name, out var a) ? a : (0, 0);

        public void Add(string name, Bitmap image, (int, int) anchor = default)
        {
            images[name] = image;
            anchors[name] = anchor;
        }

        public void AddDirectory(string directory)
        {
            var metaPath = Path.Combine(directory, "meta.json");
            JsonElement meta = default;
            bool hasMeta = File.Exists(metaPath);
            if (hasMeta) meta = JsonDocument.Parse(File.ReadAllText(metaPath)).RootElement;
            foreach (var path in Directory.GetFiles(directory, "*.png").OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                (int, int) anchor = (0, 0);
                if (hasMeta && meta.TryGetProperty(name, out var m) && m.TryGetProperty("anchor", out var a))
                    anchor = (a[0].GetInt32(), a[1].GetInt32());
                using var loaded = new Bitmap(path);
                Add(name, new Bitmap(loaded), anchor);   // salinan: file tidak terkunci
            }
        }

        public Atlas Build()
        {
            var white = new Bitmap(2, 2);
            using (var g = Graphics.FromImage(white)) g.Clear(Color.White);
            Add("white", white);
            var order = images.Keys.OrderBy(n => -images[n].Height).ThenBy(n => n, StringComparer.Ordinal).ToList();
            foreach (int size in new[] { 1024, 2048, 4096 })
            {
                var placed = Pack(order, size);
                if (placed == null) continue;
                using var sheet = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.Transparent);
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    foreach (var (name, (x, y)) in placed) g.DrawImageUnscaled(images[name], x, y);
                }
                var regions = new Dictionary<string, Region>();
                foreach (var (name, (x, y)) in placed)
                {
                    var img = images[name];
                    var (ax, ay) = anchors[name];
                    regions[name] = new Region(name, x, y, img.Width, img.Height,
                        x / (float)size, y / (float)size, (x + img.Width) / (float)size, (y + img.Height) / (float)size, ax, ay);
                }
                return new Atlas(sheet, regions);
            }
            throw new InvalidOperationException("Atlas tidak muat di 4096x4096");
        }

        private Dictionary<string, (int, int)>? Pack(List<string> order, int size)
        {
            int x = 0, y = 0, rowH = 0;
            var placed = new Dictionary<string, (int, int)>();
            foreach (var name in order)
            {
                int w = images[name].Width, h = images[name].Height;
                if (x + w + pad > size) { x = 0; y += rowH + pad; rowH = 0; }
                if (y + h + pad > size) return null;
                placed[name] = (x, y);
                x += w + pad;
                rowH = Math.Max(rowH, h);
            }
            return placed;
        }
    }

    /// <summary>Satu tekstur GL untuk semua gambar: satu draw call per pass.</summary>
    public sealed class Atlas : IDisposable
    {
        private readonly Dictionary<string, Region> regions;
        private readonly int texture;

        public Atlas(Bitmap sheet, Dictionary<string, Region> regions)
        {
            this.regions = regions;
            Size = sheet.Width;
            White = regions["white"];
            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            var data = sheet.LockBits(new Rectangle(0, 0, sheet.Width, sheet.Height), ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, sheet.Width, sheet.Height, 0,
                GLPixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            sheet.UnlockBits(data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public int Size { get; }
        public Region White { get; }

        public Region this[string name] => regions.TryGetValue(name, out var r) ? r
            : throw new KeyNotFoundException($"sprite '{name}' tidak ada di atlas");

        public bool Contains(string name) => regions.ContainsKey(name);
        public Region? Find(string name) => regions.TryGetValue(name, out var r) ? r : null;

        public void Bind() => GL.BindTexture(TextureTarget.Texture2D, texture);

        public void Dispose() => GL.DeleteTexture(texture);
    }
}
