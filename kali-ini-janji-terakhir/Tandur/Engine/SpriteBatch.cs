using OpenTK.Graphics.OpenGL4;

namespace Tandur.Engine
{
    /// <summary>Warna RGBA 0..1 (struct kecil agar tidak ada alokasi per sprite).</summary>
    public readonly record struct Rgba(float R, float G, float B, float A = 1f)
    {
        public static readonly Rgba White = new(1, 1, 1, 1);
        public Rgba WithA(float a) => this with { A = a };
        public Rgba Mul(float k) => new(R * k, G * k, B * k, A);
        public static Rgba Lerp(Rgba a, Rgba b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }

    internal static class VertexLayout
    {
        public const int Floats = 8;
        public const int Stride = Floats * sizeof(float);

        public static void Setup()
        {
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Stride, 8);
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, Stride, 16);
        }
    }

    /// <summary>Salinan GPU yang tidak berubah dari isi batch (dipakai untuk tanah per chunk).</summary>
    public sealed class StaticMesh : IDisposable
    {
        private readonly int vao, vbo, count;

        public StaticMesh(float[] vertices, int floatCount)
        {
            count = floatCount / VertexLayout.Floats;
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, floatCount * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            VertexLayout.Setup();
            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            if (count == 0) return;
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, count);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(vbo);
            GL.DeleteVertexArray(vao);
        }
    }

    /// <summary>Mengumpulkan quad bertekstur lalu menggambarnya dalam satu panggilan.
    /// Layout vertex: x, y, u, v, r, g, b, a. Satu quad = dua segitiga (6 vertex).</summary>
    public sealed class SpriteBatch : IDisposable
    {
        private readonly float[] data;
        private readonly int capacity;
        private readonly int vao, vbo;
        private int n;

        public SpriteBatch(Atlas atlas, int capacityQuads = 16384)
        {
            Atlas = atlas;
            capacity = capacityQuads * 6;
            data = new float[capacity * VertexLayout.Floats];
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            VertexLayout.Setup();
            GL.BindVertexArray(0);
        }

        public Atlas Atlas { get; }

        /// <summary>Gambar region dengan pojok kiri-atas di (x, y). shear menggeser tepi atas (angin).</summary>
        public void Quad(Region region, float x, float y, float? w = null, float? h = null, Rgba? color = null, bool flipX = false, float shear = 0f)
        {
            if (n + 6 > capacity) Flush();
            float ww = w ?? region.W, hh = h ?? region.H;
            float u0 = flipX ? region.U1 : region.U0, u1 = flipX ? region.U0 : region.U1;
            float v0 = region.V0, v1 = region.V1;
            float x1 = x + ww, y1 = y + hh;
            var c = color ?? Rgba.White;
            Vertex(x + shear, y, u0, v0, c);
            Vertex(x1 + shear, y, u1, v0, c);
            Vertex(x1, y1, u1, v1, c);
            Vertex(x + shear, y, u0, v0, c);
            Vertex(x1, y1, u1, v1, c);
            Vertex(x, y1, u0, v1, c);
        }

        /// <summary>Gambar region sehingga titik anchor-nya (misal kaki) jatuh di (wx, wy).</summary>
        public void Sprite(Region region, float wx, float wy, Rgba? color = null, bool flipX = false, float scale = 1f, float shear = 0f)
        {
            float ax = flipX ? region.W - region.Ax : region.Ax;
            Quad(region, wx - ax * scale, wy - region.Ay * scale, region.W * scale, region.H * scale, color, flipX, shear);
        }

        public void Rect(float x, float y, float w, float h, Rgba color) => Quad(Atlas.White, x, y, w, h, color);

        private void Vertex(float x, float y, float u, float v, Rgba c)
        {
            int i = n * VertexLayout.Floats;
            data[i] = x; data[i + 1] = y; data[i + 2] = u; data[i + 3] = v;
            data[i + 4] = c.R; data[i + 5] = c.G; data[i + 6] = c.B; data[i + 7] = c.A;
            n++;
        }

        public void Flush()
        {
            if (n == 0) return;
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, n * VertexLayout.Stride, data);
            GL.DrawArrays(PrimitiveType.Triangles, 0, n);
            GL.BindVertexArray(0);
            n = 0;
        }

        public void Clear() => n = 0;

        /// <summary>Jadikan semua yang sudah diantre sebuah mesh statis, lalu kosongkan batch.</summary>
        public StaticMesh Freeze()
        {
            var mesh = new StaticMesh(data, n * VertexLayout.Floats);
            n = 0;
            return mesh;
        }

        public void Dispose()
        {
            GL.DeleteBuffer(vbo);
            GL.DeleteVertexArray(vao);
        }
    }
}
