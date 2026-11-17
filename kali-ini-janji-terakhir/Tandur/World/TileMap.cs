using Tandur.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tandur.Engine;

namespace Tandur.World
{
    /// <summary>RNG gaya Python (randint inklusif, uniform, choice, shuffle) di atas System.Random.</summary>
    public sealed class Rng
    {
        private readonly Random r;
        public Rng(long seed) => r = new Random((int)(seed & 0x7FFFFFFF));
        public double Random() => r.NextDouble();
        public float Uniform(float a, float b) => a + (float)r.NextDouble() * (b - a);
        public int RandInt(int a, int b) => r.Next(a, b + 1);
        public T Choice<T>(IReadOnlyList<T> items) => items[r.Next(items.Count)];
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
    }

    public static class MathX
    {
        public static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
        public static int Mod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }
        public static int Round(float v) => (int)MathF.Round(v);
        public static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);
    }

    public sealed record ObjectDef(string Name, string Sprite, string Category, int Fw, int Fh, bool Blocking,
                                   int Frames = 1, float Fps = 0, float Sway = 0, string Lit = "");

    public sealed class PlacedObject
    {
        public PlacedObject(ObjectDef def, int x, int y, float lift = 0)
        {
            Def = def; X = x; Y = y; Lift = lift;
        }

        public ObjectDef Def { get; }
        public int X { get; }
        public int Y { get; }
        public float Lift { get; }
        public (float X, float Y) Center => (X + (Def.Fw - 1) / 2f, Y + (Def.Fh - 1) / 2f);
        public float Depth => Center.X + Center.Y;
        public (float X, float Y) WorldPosition => Iso.ToWorld(Center.X, Center.Y);

        public IEnumerable<(int, int)> Tiles()
        {
            for (int j = 0; j < Def.Fh; j++)
                for (int i = 0; i < Def.Fw; i++)
                    yield return (X + i, Y + j);
        }
    }

    /// <summary>
    /// ABSTRAKSI: segala sesuatu yang bisa dijadikan peta (peta buatan tangan atau dunia chunk tanpa batas).
    /// Pemain, NPC, hewan dan pathfinding hanya berbicara lewat kontrak ini (POLIMORFISME).
    /// </summary>
    public abstract class WorldMap
    {
        public static readonly Dictionary<char, string> GroundLegend = new()
        {
            ['g'] = "grass", ['d'] = "path", ['s'] = "sand", ['w'] = "water", ['f'] = "farmland", ['b'] = "planks", ['c'] = "stone", ['p'] = "paddy",
        };
        public static readonly HashSet<string> NotWalkable = new() { "water", "paddy" };

        public HashSet<(int, int)> Blockers { get; } = new();
        public HashSet<(int, int)> DynamicBlockers { get; set; } = new();

        public abstract string? KindAt(int x, int y);
        public virtual int HeightAt(int x, int y) => 0;
        public virtual bool Passable((int, int) a, (int, int) b) => true;
        public bool InBounds(int x, int y) => KindAt(x, y) != null;
        public float LiftAt(float gx, float gy) => HeightAt(MathX.Round(gx), MathX.Round(gy)) * ChunkWorld.LevelPx;

        public bool IsWalkable(float gx, float gy)
        {
            var t = (MathX.Round(gx), MathX.Round(gy));
            var kind = KindAt(t.Item1, t.Item2);
            return kind != null && !NotWalkable.Contains(kind) && !Blockers.Contains(t) && !DynamicBlockers.Contains(t);
        }

        /// <summary>Tanah yang bisa dipijak dan tidak ada bangunan (orang berdiri tidak dihitung).</summary>
        public bool Free(int x, int y)
        {
            var kind = KindAt(x, y);
            return kind != null && !NotWalkable.Contains(kind) && !Blockers.Contains((x, y));
        }
    }

    /// <summary>Peta buatan tangan (assets/maps/farm.json): menjadi "jangkar" dunia.</summary>
    public sealed class TileMap : WorldMap
    {
        public static readonly Dictionary<string, float> SwayByCategory = new() { ["tree"] = 1.6f, ["bush"] = 0.6f, ["flower"] = 0.9f, ["tuft"] = 1.2f, ["reed"] = 1.4f };

        private TileMap(string name, string[] ground, List<PlacedObject> objects, List<JsonObject> entities, (int, int) spawn)
        {
            Name = name;
            Ground = ground;
            Height = ground.Length;
            Width = ground[0].Length;
            Objects = objects.OrderBy(o => o.Depth).ToList();
            Entities = entities;
            Spawn = spawn;
            foreach (var o in Objects) if (o.Def.Blocking) foreach (var t in o.Tiles()) Blockers.Add(t);
        }

        public string Name { get; }
        public string[] Ground { get; }
        public int Width { get; }
        public int Height { get; }
        public List<PlacedObject> Objects { get; }
        public List<JsonObject> Entities { get; }
        public (int X, int Y) Spawn { get; }

        public override string? KindAt(int x, int y)
            => x >= 0 && y >= 0 && x < Width && y < Height ? GroundLegend[Ground[y][x]] : null;

        public static Dictionary<string, ObjectDef> LoadObjectDefs(string path)
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var defs = new Dictionary<string, ObjectDef>();
            foreach (var (name, node) in root)
            {
                var d = node!.AsObject();
                string cat = (string)d["category"]!;
                var fp = d["footprint"]?.AsArray();
                defs[name] = new ObjectDef(name, (string)d["sprite"]!, cat,
                    fp != null ? J.I(fp[0]) : 1, fp != null ? J.I(fp[1]) : 1,
                    d["blocking"] is { } b ? (bool)b : true,
                    d["frames"] is { } f ? (int)f : 1,
                    d["fps"] is { } fps ? (float)fps : 0f,
                    d["sway"] is { } s ? (float)s : SwayByCategory.GetValueOrDefault(cat),
                    d["lit"] is { } lit ? (string)lit! : "");
            }
            return defs;
        }

        public static TileMap Load(string path, Dictionary<string, ObjectDef> defs)
        {
            var data = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var ground = data["ground"]!.AsArray().Select(r => (string)r!).ToArray();
            if (ground.Length == 0 || ground.Any(r => r.Length != ground[0].Length))
                throw new InvalidDataException("baris ground harus sama panjang");
            var objects = new List<PlacedObject>();
            foreach (var e in data["objects"]?.AsArray() ?? new JsonArray())
            {
                string type = (string)e!["type"]!;
                if (!defs.TryGetValue(type, out var def)) throw new InvalidDataException($"objek tidak dikenal {type}");
                objects.Add(new PlacedObject(def, J.I(e["x"]), J.I(e["y"])));
            }
            var ents = (data["entities"]?.AsArray() ?? new JsonArray()).Select(e => e!.AsObject()).ToList();
            var sp = data["spawn"]!.AsArray();
            return new TileMap((string?)data["name"] ?? "Map", ground, objects, ents, (J.I(sp[0]), J.I(sp[1])));
        }
    }

    /// <summary>Pencarian jalur BFS 4-arah untuk penduduk desa.</summary>
    public static class Pathfind
    {
        public static List<(int X, int Y)>? FindPath(WorldMap world, (int, int) start, (int, int) goal, int margin = 14, int maxNodes = 6000)
        {
            if (start == goal) return new();
            if (!world.Free(goal.Item1, goal.Item2)) return null;
            int x0 = Math.Min(start.Item1, goal.Item1) - margin, x1 = Math.Max(start.Item1, goal.Item1) + margin;
            int y0 = Math.Min(start.Item2, goal.Item2) - margin, y1 = Math.Max(start.Item2, goal.Item2) + margin;
            var came = new Dictionary<(int, int), (int, int)> { [start] = start };
            var todo = new Queue<(int, int)>();
            todo.Enqueue(start);
            while (todo.Count > 0 && came.Count < maxNodes)
            {
                var cur = todo.Dequeue();
                if (cur == goal)
                {
                    var path = new List<(int, int)>();
                    while (cur != start) { path.Add(cur); cur = came[cur]; }
                    path.Reverse();
                    return path;
                }
                var (cx, cy) = cur;
                foreach (var nxt in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                {
                    if (came.ContainsKey(nxt) || nxt.Item1 < x0 || nxt.Item1 > x1 || nxt.Item2 < y0 || nxt.Item2 > y1) continue;
                    if (world.Free(nxt.Item1, nxt.Item2) && world.Passable(cur, nxt))
                    {
                        came[nxt] = cur;
                        todo.Enqueue(nxt);
                    }
                }
            }
            return null;
        }
    }
}
