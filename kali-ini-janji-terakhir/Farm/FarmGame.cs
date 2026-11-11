using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm
{
    using Obj = WorldObject;

    /// <summary>
    /// Main controller and state container for the Farm simulation game loop.
    /// Logic is partitioned across partial classes:
    /// - FarmGame.World.cs (World creation, terrain, zones, object placement, collision)
    /// - FarmGame.Actions.cs (Tool usage, interactions, economy, sleep)
    /// - FarmGame.Input.cs (Keyboard, mouse, viewport resizing)
    /// - FarmGame.Render.cs (World, crops, and player rendering)
    /// - FarmGame.Ui.cs (HUD, shop window, overlays, banner)
    /// - FarmGame.Save.cs (State persistence bridging with SaveSystem)
    /// </summary>
    public sealed partial class FarmGame : IDisposable
    {
        public const int GoalGold = 5000;

        // Crop data aliases pointing to CropCatalog for clean scalability
        static string[] CropName => CropCatalog.CropNames;
        static int[] Grow => CropCatalog.GrowDays;
        static int[] Sell => CropCatalog.SellPrices;
        static int[] SeedPrice => CropCatalog.SeedPrices;
        static Color[] CropCol => CropCatalog.CropColors;
        static string[] SlotName => CropCatalog.SlotNames;

        // Collections & resources
        readonly Dictionary<Point, Soil> soil = new();
        readonly List<WorldObject> objs = new();
        readonly List<Rectangle> solids = new();
        readonly List<Floater> floaters = new();
        readonly HashSet<Keys> held = new();

        readonly Font fSmall = new("Segoe UI", 9f, FontStyle.Bold);
        readonly Font fMed = new("Segoe UI", 12f, FontStyle.Bold);
        readonly Font fBig = new("Segoe UI", 26f, FontStyle.Bold);
        readonly Bitmap ground;

        // World layout & zones
        readonly Rectangle farmZone = new(17 * T, 8 * T, 7 * T, 5 * T);
        readonly Rectangle houseRect;
        readonly Rectangle pond;
        readonly Rectangle shopRect;
        readonly Rectangle binRect;
        readonly Rectangle shopZone;
        readonly Rectangle binZone;
        readonly Rectangle pondZone;

        int nextId;
        readonly HashSet<int> removedIds = new();

        // Player positioning & camera
        float px = 593, py = 790;
        int fdx = 0, fdy = 1;
        bool moving;
        float walk;
        int vw = 1280, vh = 720;
        float camX, camY;

        // Time & player attributes
        float tm = 360f;
        int day = 1;
        int gold = 100, wood, stone, slot;
        float energy = 100, water = 12;
        int waterCap = 12;
        readonly int[] seeds = { 5, 0, 0, 0 };
        readonly int[] crops = new int[4];

        // Action timing & UI states
        float anim;
        float cooldown;
        bool shopOpen;
        int shopSel;
        string msg = "";
        float msgT;
        bool won;
        bool started = true, paused;

        public bool RestartRequested, ExitRequested;

        public FarmGame(bool loadSave = false)
        {
            Sprites.Load();

            houseRect = new Rectangle(445, 488 - Sprites.House.Height, Sprites.House.Width, Sprites.House.Height);
            shopRect = new Rectangle(360, 625, Sprites.Shop.Width, Sprites.Shop.Height);
            pond = new Rectangle(1240, 390, Sprites.Pond.Width, Sprites.Pond.Height);
            binRect = new Rectangle(312, 696, Sprites.Crates.Width, Sprites.Crates.Height);
            shopZone = new Rectangle(shopRect.X + 30, shopRect.Bottom - 12, shopRect.Width - 60, 60);
            binZone = Rectangle.Inflate(binRect, 36, 36);
            pondZone = Rectangle.Inflate(pond, 30, 30);

            InitializeSolids();
            InitializeWorldObjects();
            InitializeStartingSoil();

            ground = BuildGround();

            if (loadSave)
                ApplySave();
        }

        void Say(string s)
        {
            msg = s;
            msgT = 2.5f;
        }

        void Float(string s, Color c, float x, float y)
        {
            floaters.Add(new Floater { Text = s, X = x, Y = y, Color = c });
        }

        public void Update(float dt)
        {
            anim += dt;
            if (cooldown > 0) cooldown -= dt;
            if (msgT > 0) msgT -= dt;

            foreach (var f in floaters)
            {
                f.Life -= dt;
                f.Y -= 30 * dt;
            }
            floaters.RemoveAll(f => f.Life <= 0);

            if (!started || paused || shopOpen || won) return;
            if (held.Contains(Keys.Space)) UseTool();

            tm += dt * 3f;
            if (tm >= 1440)
            {
                SleepNow();
                Say("Kamu pingsan karena larut malam.");
            }

            int dx = 0, dy = 0;
            if (held.Contains(Keys.A) || held.Contains(Keys.Left)) dx--;
            if (held.Contains(Keys.D) || held.Contains(Keys.Right)) dx++;
            if (held.Contains(Keys.W) || held.Contains(Keys.Up)) dy--;
            if (held.Contains(Keys.S) || held.Contains(Keys.Down)) dy++;

            moving = dx != 0 || dy != 0;
            if (moving)
            {
                if (dx != 0 && dy != 0) { fdx = 0; fdy = dy; } else { fdx = dx; fdy = dy; }
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float sp = (held.Contains(Keys.ShiftKey) ? 290 : 190) * dt / len;
                float nx = px + dx * sp, ny = py + dy * sp;
                if (!Blocked(nx, py)) px = nx;
                if (!Blocked(px, ny)) py = ny;
                walk += dt * 10;
            }
        }

        public void Dispose()
        {
            ground.Dispose();
            fSmall.Dispose();
            fMed.Dispose();
            fBig.Dispose();
        }
    }
}
