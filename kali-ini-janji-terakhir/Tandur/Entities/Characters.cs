using Tandur.Core;
using System.Text.Json.Nodes;
using Tandur.Engine;
using Tandur.World;

namespace Tandur.Entities
{
    /// <summary>
    /// ABSTRAKSI dasar semua makhluk di dunia (pemain, NPC, hewan): punya posisi grid, kedalaman
    /// untuk painter's algorithm, dan cara menggambar dirinya sendiri. WorldRenderer hanya
    /// memanggil Draw() tanpa tahu jenisnya (POLIMORFISME).
    /// </summary>
    public abstract class Actor
    {
        public float GX { get; set; }
        public float GY { get; set; }
        public virtual float Depth => GX + GY;
        public virtual bool Hidden => false;
        public (float X, float Y) WorldPosition => Iso.ToWorld(GX, GY);
        public abstract void Draw(SpriteBatch batch, Atlas atlas);
    }

    public enum Facing { Down, Up, Left, Right }

    /// <summary>Makhluk bertubuh yang berdiri di tile dan beranimasi. Sprite: id_{down|up|side}_{idle|walk}_n.</summary>
    public abstract class Character : Actor
    {
        public const float WalkFps = 9f, IdleFps = 1.6f;

        protected Character(string spriteId, float gx, float gy, Facing facing = Facing.Down)
        {
            SpriteId = spriteId;
            GX = gx; GY = gy;
            Facing = facing;
        }

        public string SpriteId { get; }
        public Facing Facing { get; set; }
        public bool Moving { get; set; }
        public float AnimT { get; protected set; }
        public float Lift { get; set; }
        protected virtual bool Running => false;

        public void TickAnimation(float dt) => AnimT += dt;

        public void FaceToward(float wx, float wy)
        {
            var (fx, fy) = WorldPosition;
            float dx = wx - fx, dy = wy - fy;
            if (MathF.Abs(dx) * 0.6f >= MathF.Abs(dy)) Facing = dx > 0 ? Facing.Right : Facing.Left;
            else Facing = dy > 0 ? Facing.Down : Facing.Up;
        }

        protected string Dir => Facing is Facing.Left or Facing.Right ? "side" : Facing == Facing.Down ? "down" : "up";

        public virtual string FrameName()
        {
            if (Moving)
                return Running ? $"{SpriteId}_{Dir}_run_{(int)(AnimT * WalkFps * 1.45f) % 4}" : $"{SpriteId}_{Dir}_walk_{(int)(AnimT * WalkFps) % 4}";
            int f = (int)(AnimT * IdleFps) % 2;
            // sesekali kedip/napas (frame idle_2 hasil SpriteForge) agar diam pun terasa hidup
            float cycle = (AnimT + SpriteId.Length * 0.7f) % 4.3f;
            if (cycle < 0.14f) return $"{SpriteId}_{Dir}_idle_2";
            return $"{SpriteId}_{Dir}_idle_{f}";
        }

        public override void Draw(SpriteBatch batch, Atlas atlas)
        {
            var (wx, wy) = WorldPosition;
            var region = atlas.Find(FrameName()) ?? atlas[$"{SpriteId}_{Dir}_idle_0"];
            batch.Sprite(region, MathF.Round(wx), MathF.Round(wy - Lift), flipX: Facing == Facing.Left);
        }
    }

    /// <summary>Pemain: bergerak dengan input, mengayun alat, meninggalkan debu saat berlari.</summary>
    public sealed class Player : Character
    {
        public const float Radius = 0.22f, ActionTime = 0.36f;
        private static readonly Dictionary<Facing, (int X, int Y, bool Flip)> Hand = new()
        {
            [Facing.Right] = (8, -13, false), [Facing.Left] = (-8, -13, true), [Facing.Down] = (5, -9, false), [Facing.Up] = (-5, -15, true),
        };
        private bool running;
        private float stepT;

        public Player(float gx, float gy) : base("player", gx, gy) { }

        public float ActionT { get; private set; }
        public string? ActionIcon { get; private set; }
        public bool Acting => ActionT > 0;
        protected override bool Running => running;
        public bool RunningNow => Moving && running;

        /// <summary>Dipanggil saat kaki menapak ketika berlari (untuk efek debu).</summary>
        public event Action<float, float>? Footstep;

        public void StartAction(string? icon = null)
        {
            ActionT = ActionTime;
            ActionIcon = icon;
            Moving = false;
        }

        private bool CanStand(float gx, float gy, WorldMap map)
        {
            var here = (MathX.Round(GX), MathX.Round(GY));
            var there = (MathX.Round(gx), MathX.Round(gy));
            if (!map.Passable(here, there)) return false;
            foreach (float ox in new[] { -Radius, Radius })
                foreach (float oy in new[] { -Radius, Radius })
                {
                    if (!map.IsWalkable(gx + ox, gy + oy)) return false;
                    if (!map.Passable(there, (MathX.Round(gx + ox), MathX.Round(gy + oy)))) return false;
                }
            return true;
        }

        public void Update(float dt, InputState inp, WorldMap map, bool frozen = false)
        {
            TickAnimation(dt);
            ActionT = Math.Max(0, ActionT - dt);
            Moving = false;
            if (frozen || Acting) return;
            int mx = (inp.Held(InputAction.Right) ? 1 : 0) - (inp.Held(InputAction.Left) ? 1 : 0);
            int my = (inp.Held(InputAction.Down) ? 1 : 0) - (inp.Held(InputAction.Up) ? 1 : 0);
            if (mx == 0 && my == 0) return;
            running = inp.Held(InputAction.Run);
            float len = MathF.Sqrt(mx * mx + my * my);
            float speed = Balance.WalkSpeed * (running ? Balance.RunMult : 1f);
            var (dgx, dgy) = Iso.ToGrid(mx / len * speed * dt, my / len * speed * dt);
            if (CanStand(GX + dgx, GY, map)) { GX += dgx; Moving = true; }
            if (CanStand(GX, GY + dgy, map)) { GY += dgy; Moving = true; }
            Facing = mx != 0 ? (mx > 0 ? Facing.Right : Facing.Left) : (my > 0 ? Facing.Down : Facing.Up);
            if (Moving && running)
            {
                stepT -= dt;
                if (stepT <= 0)
                {
                    stepT = 0.16f;
                    var (wx, wy) = WorldPosition;
                    Footstep?.Invoke(wx, wy - Lift);
                }
            }
        }

        public (float X, float Y) FrontWorldPoint()
        {
            var (wx, wy) = WorldPosition;
            var (dx, dy) = Facing switch { Facing.Right => (22, 0), Facing.Left => (-22, 0), Facing.Down => (0, 14), _ => (0, -14) };
            return (wx + dx, wy + dy);
        }

        public override string FrameName()
        {
            if (Acting)
            {
                float p = 1 - ActionT / ActionTime;
                int f = p < 0.45f ? 0 : p < 0.75f ? 1 : 2;
                return $"player_{Dir}_act_{f}";
            }
            return base.FrameName();
        }

        public override void Draw(SpriteBatch batch, Atlas atlas)
        {
            var (wx, wy) = WorldPosition;
            wy -= Lift;
            bool flip = Facing == Facing.Left;
            bool behind = Acting && ActionIcon != null && Facing == Facing.Up;
            if (behind) DrawTool(batch, atlas, wx, wy);
            var region = atlas.Find(FrameName()) ?? atlas[$"player_{Dir}_idle_0"];
            batch.Sprite(region, MathF.Round(wx), MathF.Round(wy), flipX: flip);
            if (Acting && ActionIcon != null && !behind) DrawTool(batch, atlas, wx, wy);
        }

        private void DrawTool(SpriteBatch batch, Atlas atlas, float wx, float wy)
        {
            var (hx, hy, flip) = Hand[Facing];
            float p = 1 - ActionT / ActionTime;
            float swing = p < 0.6f ? -6 + 12 * p : 4;
            batch.Quad(atlas[ActionIcon!], MathF.Round(wx + hx - 8), MathF.Round(wy + hy + swing - 8), flipX: flip);
        }
    }

    /// <summary>Penduduk desa dengan jadwal harian: berjalan (BFS) antar titik, kadang berkeliaran.</summary>
    public sealed class Npc : Character
    {
        public const float PersonalSpace = 1.5f;
        private readonly List<(int M, int X, int Y, Facing F)> schedule;
        private readonly float wander, speed;
        private readonly Rng rng;
        private (int X, int Y)? goal;
        private List<(int X, int Y)> path = new();
        private float pause;
        private (int X, int Y, Facing F)? spot;

        public Npc(JsonObject e) : base((string)e["sprite"]!, J.F(e["x"]), J.F(e["y"]), ParseFacing((string?)e["facing"]))
        {
            Id = (string)e["id"]!;
            Name = (string?)e["name"] ?? Id;
            Script = (string)e["script"]!;
            Prompt = (string?)e["prompt"] ?? "Bicara";
            int sum = Id.Sum(c => c);
            AnimT = sum % 100 / 50f;
            Home = (J.I(e["x"]), J.I(e["y"]), Facing);
            schedule = (e["schedule"] as JsonArray)?.Select(s => (J.I(s![0]), J.I(s[1]), J.I(s[2]), ParseFacing((string)s[3]!)))
                       .OrderBy(s => s.Item1).ToList() ?? new();
            wander = J.Fn(e["wander"]) ?? 0;
            speed = J.Fn(e["speed"]) ?? 1.15f;
            rng = new Rng(sum);
        }

        public string Id { get; }
        public string Name { get; }
        public string Script { get; }
        public string Prompt { get; }
        public (int X, int Y, Facing F) Home { get; }

        public static Facing ParseFacing(string? f) => f switch { "up" => Facing.Up, "left" => Facing.Left, "right" => Facing.Right, _ => Facing.Down };

        public (int X, int Y, Facing F) SpotFor(float minutes)
        {
            if (schedule.Count == 0) return Home;
            var last = schedule[^1];
            var s = (last.X, last.Y, last.F);
            foreach (var e in schedule) if (minutes >= e.M) s = (e.X, e.Y, e.F);
            return s;
        }

        public void Snap(float minutes)
        {
            var s = SpotFor(minutes);
            GX = s.X; GY = s.Y; Facing = s.F;
            goal = null; path = new(); Moving = false; spot = s;
        }

        public void Update(float dt, WorldMap? world, float? minutes, Player? player)
        {
            TickAnimation(dt);
            if (world == null || minutes == null) return;
            Moving = false;
            if (player != null && MathX.Hypot(player.GX - GX, player.GY - GY) < PersonalSpace) return;
            var s = SpotFor(minutes.Value);
            var here = (MathX.Round(GX), MathX.Round(GY));
            if (spot != s) { spot = s; goal = null; pause = 0; }
            if (goal == null)
            {
                goal = (s.X, s.Y);
                path = Pathfind.FindPath(world, here, goal.Value) ?? new();
            }
            if (path.Count == 0)
            {
                if (here == (s.X, s.Y) && wander == 0) Facing = s.F;
                if (wander > 0)
                {
                    pause -= dt;
                    if (pause <= 0)
                    {
                        pause = rng.Uniform(1, 4);
                        int tx = s.X + MathX.Round(rng.Uniform(-wander, wander)), ty = s.Y + MathX.Round(rng.Uniform(-wander, wander));
                        if (world.Free(tx, ty) && world.HeightAt(tx, ty) == 0)
                            path = Pathfind.FindPath(world, here, (tx, ty), 6, 800) ?? new();
                    }
                }
                return;
            }
            var (px, py) = path[0];
            float dx = px - GX, dy = py - GY, dist = MathX.Hypot(dx, dy), step = speed * dt;
            if (dist <= step) { GX = px; GY = py; path.RemoveAt(0); }
            else { GX += dx / dist * step; GY += dy / dist * step; }
            if (dist > 0.05f) { var (twx, twy) = Iso.ToWorld(px, py); FaceToward(twx, twy); }
            Moving = true;
        }
    }

    /// <summary>Apa pun yang bisa diajak berinteraksi (E): NPC, hewan, atau objek peta (papan, sumur...).</summary>
    public sealed class Interactable
    {
        public Interactable(string id, float gx, float gy, string script, string prompt, Npc? npc = null, float lift = 40, Actor? owner = null)
        {
            Id = id; GX = gx; GY = gy; Script = script; Prompt = prompt; Npc = npc; LiftPx = lift; Owner = owner;
        }

        public string Id { get; }
        public float GX { get; set; }
        public float GY { get; set; }
        public string Script { get; }
        public string Prompt { get; }
        public Npc? Npc { get; }
        public float LiftPx { get; }
        public Actor? Owner { get; }
    }
}
