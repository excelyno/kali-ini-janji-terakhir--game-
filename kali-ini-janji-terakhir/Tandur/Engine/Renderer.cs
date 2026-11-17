using OpenTK.Graphics.OpenGL4;

namespace Tandur.Engine
{
    /// <summary>Lampu titik di ruang dunia (piksel seni).</summary>
    public readonly record struct Light(float X, float Y, float Radius, float Intensity, float R, float G, float B);

    /// <summary>Parameter efek layar untuk pass komposit dunia, diisi oleh scene tiap frame.</summary>
    public sealed class PostFx
    {
        public float Bloom = 0.25f;
        public float Threshold = 0.85f;
        public float Vignette = 0.35f;
        public float Heat;
        public float Saturation = 1.05f;
        public float Contrast = 1.04f;
        public float GradeR = 1f, GradeG = 1f, GradeB = 1f;
        public float Wet;

        public void Reset()
        {
            Bloom = 0.25f; Threshold = 0.85f; Vignette = 0.35f; Heat = 0; Saturation = 1.05f; Contrast = 1.04f;
            GradeR = GradeG = GradeB = 1f; Wet = 0;
        }
    }

    /// <summary>Framebuffer offscreen dengan satu tekstur warna.</summary>
    internal sealed class RenderTarget : IDisposable
    {
        public int Fbo, Tex, W, H;

        public void Ensure(int w, int h, bool linear)
        {
            if (w == W && h == H && Fbo != 0) return;
            Dispose();
            W = w; H = h;
            Tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            var f = linear ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, f);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, f);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            Fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Tex, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Dispose()
        {
            if (Fbo != 0) GL.DeleteFramebuffer(Fbo);
            if (Tex != 0) GL.DeleteTexture(Tex);
            Fbo = Tex = 0;
        }
    }

    /// <summary>
    /// Pemilik shader, sprite batch, dan dua mode tampilan (dunia / UI).
    /// Dunia digambar ke framebuffer offscreen lalu diproses (bloom, grading, vinyet);
    /// UI selalu digambar tajam langsung ke layar di atasnya.
    /// </summary>
    public sealed class Renderer : IDisposable
    {
        public const int WorldViewHeight = 360;
        public const int UiVirtualHeight = 720;

        private readonly SpriteShader sprite = new();
        private readonly BrightShader bright = new();
        private readonly BlurShader blur = new();
        private readonly CompositeShader composite = new();
        private readonly RenderTarget scene = new(), bloomA = new(), bloomB = new();
        private readonly int emptyVao = GL.GenVertexArray();
        private readonly float[] lightPos = new float[SpriteShader.MaxLights * 4];
        private readonly float[] lightCol = new float[SpriteShader.MaxLights * 3];
        private bool inWorld;
        private float time;

        public Renderer(Atlas atlas)
        {
            Atlas = atlas;
            Batch = new SpriteBatch(atlas);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        public Atlas Atlas { get; }
        public SpriteBatch Batch { get; }
        public int Width { get; private set; } = 1;
        public int Height { get; private set; } = 1;
        public List<Light> Lights { get; set; } = new();
        public PostFx Post { get; } = new();
        public bool PostEnabled { get; set; } = true;
        public (float R, float G, float B) ClearColor { get; set; } = (0.09f, 0.14f, 0.17f);

        public void Resize(int w, int h)
        {
            Width = Math.Max(1, w);
            Height = Math.Max(1, h);
        }

        public int WorldZoom => Math.Max(1, Height / WorldViewHeight);
        public (float W, float H) WorldViewSize => (Width / (float)WorldZoom, Height / (float)WorldZoom);
        public float UiZoom => Height / (float)UiVirtualHeight;
        public (float W, float H) UiSize => (Width / UiZoom, UiVirtualHeight);

        public void BeginFrame(float dt)
        {
            time += dt;
            inWorld = false;
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, Width, Height);
            GL.ClearColor(ClearColor.R, ClearColor.G, ClearColor.B, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Batch.Clear();
            BindSprite();
        }

        private void BindSprite()
        {
            sprite.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            Atlas.Bind();
            sprite.Set("u_tex", 0);
            sprite.Set("u_screen", Width, Height);
        }

        private void SetView(float cx, float cy, float zoom, Rgba tint, IReadOnlyList<Light> lights)
        {
            Batch.Flush();
            sprite.Set("u_camera", cx, cy);
            sprite.Set("u_zoom", zoom);
            sprite.Set("u_tint", tint.R, tint.G, tint.B, tint.A);
            int n = Math.Min(lights.Count, SpriteShader.MaxLights);
            sprite.Set("u_light_count", n);
            sprite.Set("u_pixel", 1f);
            for (int i = 0; i < n; i++)
            {
                var l = lights[i];
                lightPos[i * 4] = l.X; lightPos[i * 4 + 1] = l.Y; lightPos[i * 4 + 2] = l.Radius; lightPos[i * 4 + 3] = l.Intensity;
                lightCol[i * 3] = l.R; lightCol[i * 3 + 1] = l.G; lightCol[i * 3 + 2] = l.B;
            }
            if (n > 0)
            {
                sprite.Set4v("u_lights", lightPos, n);
                sprite.Set3v("u_light_colors", lightCol, n);
            }
        }

        /// <summary>Masuk mode dunia (zoom bulat). Mengembalikan posisi kamera yang dibulatkan.</summary>
        public (float X, float Y) ViewWorld(Camera camera, Rgba tint)
        {
            if (!inWorld && PostEnabled)
            {
                Batch.Flush();
                scene.Ensure(Width, Height, false);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, scene.Fbo);
                GL.Viewport(0, 0, Width, Height);
                GL.ClearColor(ClearColor.R, ClearColor.G, ClearColor.B, 1f);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                inWorld = true;
            }
            var (vw, vh) = WorldViewSize;
            var (cx, cy) = camera.PixelPosition;
            float hw = vw / 2 + 120, hh = vh / 2 + 120;
            var near = Lights.Where(l => Math.Abs(l.X - cx) < hw + l.Radius && Math.Abs(l.Y - cy) < hh + l.Radius)
                             .OrderBy(l => Math.Abs(l.X - cx) + Math.Abs(l.Y - cy)).ToList();
            SetView(cx, cy, WorldZoom, tint, near);
            return (cx, cy);
        }

        public void ViewUi(Rgba? tint = null)
        {
            if (inWorld) Resolve();
            var (w, h) = UiSize;
            SetView(w / 2, h / 2, UiZoom, tint ?? Rgba.White, Array.Empty<Light>());
        }

        public void DrawMesh(StaticMesh mesh)
        {
            Batch.Flush();
            mesh.Draw();
        }

        /// <summary>Jalankan rantai post-processing dan tulis hasil dunia ke layar.</summary>
        private void Resolve()
        {
            Batch.Flush();
            inWorld = false;
            int bw = Math.Max(1, Width / 4), bh = Math.Max(1, Height / 4);
            bloomA.Ensure(bw, bh, true);
            bloomB.Ensure(bw, bh, true);
            GL.Disable(EnableCap.Blend);
            GL.BindVertexArray(emptyVao);
            GL.ActiveTexture(TextureUnit.Texture0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, bloomA.Fbo);
            GL.Viewport(0, 0, bw, bh);
            bright.Use();
            bright.Set("u_tex", 0);
            bright.Set("u_threshold", Post.Threshold);
            GL.BindTexture(TextureTarget.Texture2D, scene.Tex);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            blur.Use();
            blur.Set("u_tex", 0);
            for (int pass = 0; pass < 2; pass++)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, bloomB.Fbo);
                blur.Set("u_dir", 1.5f / bw, 0);
                GL.BindTexture(TextureTarget.Texture2D, bloomA.Tex);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, bloomA.Fbo);
                blur.Set("u_dir", 0, 1.5f / bh);
                GL.BindTexture(TextureTarget.Texture2D, bloomB.Tex);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, Width, Height);
            composite.Use();
            composite.Set("u_scene", 0);
            composite.Set("u_bloom", 1);
            composite.Set("u_bloom_k", Post.Bloom);
            composite.Set("u_vignette", Post.Vignette);
            composite.Set("u_heat", Post.Heat);
            composite.Set("u_sat", Post.Saturation);
            composite.Set("u_contrast", Post.Contrast);
            GL.Uniform3(GL.GetUniformLocation(composite.Handle, "u_grade"), Post.GradeR, Post.GradeG, Post.GradeB);
            composite.Set("u_time", time);
            composite.Set("u_wet", Post.Wet);
            composite.Set("u_res", Width, Height);
            GL.BindTexture(TextureTarget.Texture2D, scene.Tex);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, bloomA.Tex);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.Blend);
            BindSprite();
        }

        public void EndFrame()
        {
            Batch.Flush();
            if (inWorld) Resolve();
            GL.UseProgram(0);
        }

        public void Dispose()
        {
            Batch.Dispose();
            sprite.Dispose(); bright.Dispose(); blur.Dispose(); composite.Dispose();
            scene.Dispose(); bloomA.Dispose(); bloomB.Dispose();
            GL.DeleteVertexArray(emptyVao);
        }
    }
}
