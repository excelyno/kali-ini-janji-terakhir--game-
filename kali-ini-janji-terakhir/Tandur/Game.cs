using System.Text.Json.Nodes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Tandur.Core;
using Tandur.Engine;
using Tandur.Vn;
using Tandur.World;

namespace Tandur
{
    /// <summary>
    /// ABSTRAKSI layar/mode (judul, gameplay, dialog...). Hanya scene teratas yang di-update.
    /// Transparent = scene di bawahnya tetap digambar (overlay).
    /// </summary>
    public abstract class Scene
    {
        public TandurGame Game { get; internal set; } = null!;
        public virtual bool Transparent => false;
        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void Update(float dt) { }
        public virtual void Draw(Renderer gfx) { }
    }

    public sealed class SceneStack
    {
        private readonly TandurGame game;
        private readonly List<Scene> stack = new();

        public SceneStack(TandurGame game) => this.game = game;

        public Scene? Top => stack.Count > 0 ? stack[^1] : null;
        public int Count => stack.Count;

        public void Push(Scene s)
        {
            s.Game = game;
            stack.Add(s);
            s.OnEnter();
        }

        public Scene Pop()
        {
            var s = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            s.OnExit();
            return s;
        }

        public void Replace(Scene s)
        {
            while (stack.Count > 0) Pop();
            Push(s);
        }

        public void Update(float dt) => Top?.Update(dt);

        public void Draw(Renderer gfx)
        {
            int first = stack.Count - 1;
            while (first > 0 && stack[first].Transparent) first--;
            foreach (var s in stack.Skip(Math.Max(first, 0)).ToList()) s.Draw(gfx);
        }
    }

    /// <summary>Opsi baris perintah untuk pengujian otomatis (screenshot tanpa interaksi).</summary>
    public sealed class LaunchOptions
    {
        public string? ShotPath;
        public float ShotAfter = 3f;
        public bool AutoPlay;
        public float Minutes = -1;
        public int Act = -1;
        public string Walk = "";
        public bool Hidden;

        public static LaunchOptions Parse(string[] args)
        {
            var o = new LaunchOptions();
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "--shot": o.ShotPath = args[++i]; break;
                    case "--after": o.ShotAfter = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--play": o.AutoPlay = true; break;
                    case "--time": o.Minutes = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--act": o.Act = int.Parse(args[++i]); break;
                    case "--walk": o.Walk = args[++i]; break;
                    case "--hidden": o.Hidden = true; break;
                }
            return o;
        }
    }

    /// <summary>Jendela, setup GL, layanan bersama, dan loop fixed-timestep 60 Hz.</summary>
    public sealed class TandurGame : GameWindow
    {
        public const string WindowTitle = "TANDUR - Where the Seasons Begin";
        public const float FixedDt = 1f / 60f;
        public static readonly string AssetRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Tandur");

        private float accumulator, fpsT, elapsed;
        private int fpsN;
        private bool running = true;

        public TandurGame(LaunchOptions opts) : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new Vector2i(1280, 720),
            Title = WindowTitle,
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            StartVisible = !opts.Hidden,
        })
        {
            Options = opts;
            VSync = VSyncMode.On;
        }

        public LaunchOptions Options { get; }
        public Atlas Atlas { get; private set; } = null!;
        public BitmapFont Font { get; private set; } = null!;
        public Renderer Gfx { get; private set; } = null!;
        public InputState Input { get; } = new();
        public GameState State { get; private set; } = new();
        public SaveManager Saves { get; } = new();
        public JsonObject? PendingLoad { get; set; }
        public EventBus Events { get; } = new();
        public GameClock Clock { get; private set; } = new(Balance.DayRealSeconds);
        public VnLibrary Vn { get; private set; } = null!;
        public SceneStack Scenes { get; private set; } = null!;
        public float Time => elapsed;

        public static string Asset(params string[] parts) => Path.Combine(new[] { AssetRoot }.Concat(parts).ToArray());

        protected override void OnLoad()
        {
            base.OnLoad();
            var builder = new AtlasBuilder();
            builder.AddDirectory(Asset("sprites"));
            SpriteForge.Forge(builder);
            var metrics = BitmapFont.Bake(builder, Asset("fonts", "PixelifySans.ttf"));
            Atlas = builder.Build();
            foreach (var img in builder.Images.Values) img.Dispose();
            Font = new BitmapFont(Atlas, metrics);
            Gfx = new Renderer(Atlas);
            Gfx.Resize(FramebufferSize.X, FramebufferSize.Y);
            Vn = new VnLibrary(Asset("vn"));
            Scenes = new SceneStack(this);
            CursorState = CursorState.Hidden;
            Scenes.Push(new global::Tandur.Scenes.TitleScene());
            if (Options.AutoPlay)
            {
                NewGame();
                Scenes.Replace(new global::Tandur.Scenes.PlayScene());
            }
        }

        public (float X, float Y) MouseUi()
        {
            var (cx, cy) = Input.Cursor;
            float z = Gfx.UiZoom;
            return (cx * Gfx.Width / Math.Max(ClientSize.X, 1) / z, cy * Gfx.Height / Math.Max(ClientSize.Y, 1) / z);
        }

        public (float X, float Y) MouseFramebuffer()
        {
            var (cx, cy) = Input.Cursor;
            return (cx * Gfx.Width / (float)Math.Max(ClientSize.X, 1), cy * Gfx.Height / (float)Math.Max(ClientSize.Y, 1));
        }

        public void NewGame()
        {
            State = new GameState();
            State.Set("world_seed", Random.Shared.Next(1, 1 << 30));
            Clock = new GameClock(Balance.DayRealSeconds);
            PendingLoad = null;
        }

        public bool LoadGame()
        {
            var data = Saves.Load();
            if (data == null) return false;
            State = GameState.FromJson((JsonObject)data["state"]!);
            Clock = new GameClock(Balance.DayRealSeconds);
            Clock.Load(data["clock"]);
            PendingLoad = data;
            return true;
        }

        public void Quit() => running = false;

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            float frame = Math.Min((float)e.Time, 0.25f);
            accumulator += frame;
            elapsed += frame;
            while (accumulator >= FixedDt && Scenes.Count > 0)
            {
                Scenes.Update(FixedDt);
                Input.EndStep();
                accumulator -= FixedDt;
            }
            if (!running || Scenes.Count == 0) { Close(); return; }

            Gfx.BeginFrame(frame);
            Scenes.Draw(Gfx);
            if (Input.CursorSeen && Options.ShotPath == null)
            {
                Gfx.ViewUi();
                var (mx, my) = MouseUi();
                var region = Atlas["ui_cursor"];
                float squash = Input.MouseDown ? 0.88f : 1f;
                Gfx.Batch.Quad(region, mx - 13, my - 1, region.W * 3 * squash, region.H * 3 * squash);
            }
            Gfx.EndFrame();

            if (Options.ShotPath != null && elapsed >= Options.ShotAfter)
            {
                Screenshot(Options.ShotPath);
                Close();
                return;
            }
            SwapBuffers();

            fpsT += frame; fpsN++;
            if (fpsT >= 1f)
            {
                Title = $"{WindowTitle}  |  {fpsN / fpsT:0} FPS";
                fpsT = 0; fpsN = 0;
            }
        }

        private void Screenshot(string path)
        {
            int w = Gfx.Width, h = Gfx.Height;
            var px = new byte[w * h * 4];
            GL.ReadPixels(0, 0, w, h, PixelFormat.Bgra, PixelType.UnsignedByte, px);
            using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                System.Runtime.InteropServices.Marshal.Copy(px, (h - 1 - y) * w * 4, data.Scan0 + y * data.Stride, w * 4);
            bmp.UnlockBits(data);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);
            Gfx?.Resize(e.Width, e.Height);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e) { base.OnKeyDown(e); Input.KeyDown(e.Key, e.IsRepeat); }
        protected override void OnKeyUp(KeyboardKeyEventArgs e) { base.OnKeyUp(e); Input.KeyUp(e.Key); }
        protected override void OnMouseMove(MouseMoveEventArgs e) { base.OnMouseMove(e); Input.MouseMove(e.X, e.Y); }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButton.Left) Input.MouseButton(true);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButton.Left) Input.MouseButton(false);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e) { base.OnMouseWheel(e); Input.Wheel(e.OffsetY); }

        protected override void OnFocusedChanged(FocusedChangedEventArgs e)
        {
            base.OnFocusedChanged(e);
            if (!e.IsFocused) Input.LostFocus();
        }

        protected override void OnUnload()
        {
            Gfx?.Dispose();
            Atlas?.Dispose();
            base.OnUnload();
        }
    }
}
