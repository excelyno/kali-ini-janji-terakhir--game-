using OpenTK.Graphics.OpenGL4;

namespace Tandur.Engine
{
    /// <summary>
    /// ABSTRAKSI + PEWARISAN: kompilasi, linking dan cache lokasi uniform ada di sini sekali;
    /// tiap shader konkret hanya memberi sumber GLSL-nya.
    /// </summary>
    public abstract class ShaderProgram : IDisposable
    {
        private readonly Dictionary<string, int> locations = new();

        protected ShaderProgram()
        {
            int vs = Compile(VertexSource, ShaderType.VertexShader);
            int fs = Compile(FragmentSource, ShaderType.FragmentShader);
            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vs);
            GL.AttachShader(Handle, fs);
            GL.LinkProgram(Handle);
            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException("Shader link gagal:\n" + GL.GetProgramInfoLog(Handle));
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
        }

        public int Handle { get; }
        protected abstract string VertexSource { get; }
        protected abstract string FragmentSource { get; }

        public void Use() => GL.UseProgram(Handle);

        protected int Loc(string name)
        {
            if (!locations.TryGetValue(name, out int l))
                locations[name] = l = GL.GetUniformLocation(Handle, name);
            return l;
        }

        public void Set(string name, int v) => GL.Uniform1(Loc(name), v);
        public void Set(string name, float v) => GL.Uniform1(Loc(name), v);
        public void Set(string name, float x, float y) => GL.Uniform2(Loc(name), x, y);
        public void Set(string name, float x, float y, float z, float w) => GL.Uniform4(Loc(name), x, y, z, w);
        public void Set4v(string name, float[] v, int count) => GL.Uniform4(Loc(name), count, v);
        public void Set3v(string name, float[] v, int count) => GL.Uniform3(Loc(name), count, v);

        private static int Compile(string src, ShaderType type)
        {
            int s = GL.CreateShader(type);
            GL.ShaderSource(s, src);
            GL.CompileShader(s);
            GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"Shader {type} gagal:\n" + GL.GetShaderInfoLog(s));
            return s;
        }

        public void Dispose() => GL.DeleteProgram(Handle);
    }

    /// <summary>Shader sprite TANDUR: tint ambient + sampai 24 lampu titik (elips isometrik, berpita seperti pixel art).</summary>
    public sealed class SpriteShader : ShaderProgram
    {
        public const int MaxLights = 24;

        protected override string VertexSource => @"#version 330 core
layout (location = 0) in vec2 a_position;
layout (location = 1) in vec2 a_uv;
layout (location = 2) in vec4 a_color;
uniform vec2  u_camera;
uniform vec2  u_screen;
uniform float u_zoom;
out vec2 v_uv;
out vec4 v_color;
out vec2 v_world;
void main() {
    v_uv = a_uv;
    v_color = a_color;
    v_world = a_position;
    vec2 p = (a_position - u_camera) * u_zoom / (u_screen * 0.5);
    gl_Position = vec4(p.x, -p.y, 0.0, 1.0);
}";

        protected override string FragmentSource => @"#version 330 core
in vec2 v_uv;
in vec4 v_color;
in vec2 v_world;
#define MAX_LIGHTS 24
uniform sampler2D u_tex;
uniform vec4 u_tint;
uniform int  u_light_count;
uniform vec4 u_lights[MAX_LIGHTS];
uniform vec3 u_light_colors[MAX_LIGHTS];
uniform float u_pixel;
out vec4 FragColor;
void main() {
    vec4 tex = texture(u_tex, v_uv) * v_color;
    if (tex.a < 0.004) discard;
    vec3 light = u_tint.rgb;
    if (u_light_count > 0) {
        vec2 p = floor(v_world / u_pixel) * u_pixel;
        vec3 add = vec3(0.0);
        for (int i = 0; i < MAX_LIGHTS; ++i) {
            if (i >= u_light_count) break;
            vec4 L = u_lights[i];
            vec2 d = (p - L.xy) * vec2(1.0, 2.0);
            float f = clamp(1.0 - length(d) / L.z, 0.0, 1.0);
            f = f * f;
            f = floor(f * 6.0 + 0.5) / 6.0;
            add += u_light_colors[i] * L.w * f;
        }
        light = min(light + add, vec3(1.35));
    }
    FragColor = vec4(tex.rgb * light, tex.a * u_tint.a);
}";
    }

    /// <summary>Segitiga layar penuh untuk pass post-processing.</summary>
    public abstract class FullscreenShader : ShaderProgram
    {
        protected override string VertexSource => @"#version 330 core
out vec2 v_uv;
void main() {
    vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    v_uv = p;
    gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}";
    }

    /// <summary>Bright-pass: ambil bagian terang untuk bloom (cahaya lampu malam, kilau air).</summary>
    public sealed class BrightShader : FullscreenShader
    {
        protected override string FragmentSource => @"#version 330 core
in vec2 v_uv;
uniform sampler2D u_tex;
uniform float u_threshold;
out vec4 FragColor;
void main() {
    vec3 c = texture(u_tex, v_uv).rgb;
    float l = max(c.r, max(c.g, c.b));
    float k = smoothstep(u_threshold, u_threshold + 0.25, l);
    FragColor = vec4(c * k, 1.0);
}";
    }

    /// <summary>Blur gaussian 9-tap terpisah (horizontal lalu vertikal).</summary>
    public sealed class BlurShader : FullscreenShader
    {
        protected override string FragmentSource => @"#version 330 core
in vec2 v_uv;
uniform sampler2D u_tex;
uniform vec2 u_dir;
out vec4 FragColor;
void main() {
    float w[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    vec3 c = texture(u_tex, v_uv).rgb * w[0];
    for (int i = 1; i < 5; ++i) {
        c += texture(u_tex, v_uv + u_dir * float(i)).rgb * w[i];
        c += texture(u_tex, v_uv - u_dir * float(i)).rgb * w[i];
    }
    FragColor = vec4(c, 1.0);
}";
    }

    /// <summary>
    /// Komposit akhir dunia: bloom, color grading per musim/cuaca, udara panas bergetar
    /// saat kemarau, kilau hujan, vinyet, dan dither halus agar gradien tidak bergaris.
    /// </summary>
    public sealed class CompositeShader : FullscreenShader
    {
        protected override string FragmentSource => @"#version 330 core
in vec2 v_uv;
uniform sampler2D u_scene;
uniform sampler2D u_bloom;
uniform float u_bloom_k;
uniform float u_vignette;
uniform float u_heat;
uniform float u_sat;
uniform float u_contrast;
uniform vec3  u_grade;
uniform float u_time;
uniform float u_wet;
uniform vec2  u_res;
out vec4 FragColor;
float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
void main() {
    vec2 uv = v_uv;
    if (u_heat > 0.0) {
        float w = sin(uv.y * 90.0 + u_time * 3.1) * 0.5 + sin(uv.y * 37.0 - u_time * 1.7) * 0.5;
        uv.x += w * u_heat * 0.0016 * (1.0 - uv.y * 0.6);
    }
    vec3 c = texture(u_scene, uv).rgb;
    vec3 b = texture(u_bloom, uv).rgb;
    c += b * u_bloom_k;
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    c = mix(vec3(l), c, u_sat);
    c = (c - 0.5) * u_contrast + 0.5;
    c *= u_grade;
    if (u_wet > 0.0) {
        float streak = smoothstep(0.92, 1.0, sin((uv.x * 140.0 + uv.y * 30.0) + u_time * 0.7));
        c += vec3(0.06, 0.08, 0.10) * streak * u_wet * smoothstep(0.3, 1.0, uv.y);
        c = mix(c, c * vec3(0.92, 0.96, 1.04), u_wet);
    }
    vec2 d = v_uv - 0.5;
    float v = 1.0 - dot(d, d) * u_vignette * 1.6;
    c *= clamp(v, 0.0, 1.0);
    c += (hash(v_uv * u_res + fract(u_time)) - 0.5) / 255.0;
    FragColor = vec4(c, 1.0);
}";
    }
}
