using Tandur.Core;
using System.Text.Json.Nodes;
using Tandur.Engine;

namespace Tandur.World
{
    public enum ChunkState { Hidden, Revealing, Revealed }

    /// <summary>Isi satu chunk hasil generator (fungsi murni dari seed, cx, cy).</summary>
    public sealed class GenChunk
    {
        public int Cx, Cy;
        public string Biome = "";
        public string[] Ground = Array.Empty<string>();
        public List<(string Type, int X, int Y)> Objects = new();
        public List<JsonObject> Entities = new();
        public int[,] Height = new int[ChunkWorld.Size, ChunkWorld.Size];
        public Dictionary<(int, int), (int, int)> Ramps = new();
    }

    public sealed class Chunk
    {
        public Chunk(int cx, int cy, string biome, string[] ground, List<PlacedObject> objects, List<JsonObject> entities,
                     bool anchor = false, int[,]? height = null, Dictionary<(int, int), (int, int)>? ramps = null)
        {
            Cx = cx; Cy = cy; Biome = biome; Ground = ground;
            Height = height ?? new int[ChunkWorld.Size, ChunkWorld.Size];
            Ramps = ramps ?? new();
            Raised = Height.Cast<int>().Any(h => h > 0);
            Objects = objects.OrderBy(o => o.Depth).ToList();
            Entities = entities;
            Anchor = anchor;
            State = anchor ? ChunkState.Revealed : ChunkState.Hidden;
        }

        public int Cx { get; }
        public int Cy { get; }
        public string Biome { get; }
        public string[] Ground { get; }
        public int[,] Height { get; }        // [y, x]
        public Dictionary<(int, int), (int, int)> Ramps { get; }
        public bool Raised { get; }
        public List<PlacedObject> Objects { get; }
        public List<JsonObject> Entities { get; }
        public bool Anchor { get; }
        public ChunkState State { get; set; }
        public float T { get; set; }
        public StaticMesh? Mesh { get; set; }
        public bool Dirty { get; set; } = true;
        public List<(float X, float Y, int Hash)> Water { get; } = new();
        public bool Visible => State != ChunkState.Hidden;
        public string Name => ChunkWorld.BiomeNames.GetValueOrDefault(Biome, Biome);
    }

    /// <summary>
    /// Dunia tanpa batas dari chunk 16x16. Peta buatan tangan menjadi jangkar (chunk 0..2), sisanya
    /// dibangkitkan dari seed. Chunk tersembunyi "naik" saat pemain mendekat; cincin terluar dikunci kabut.
    /// Tile punya level tinggi 0..2; tangga satu-satunya jalan naik.
    /// </summary>
    public sealed class ChunkWorld : WorldMap
    {
        public const int Size = 16;
        public const int RevealRadius = 9;
        public const float RevealSeconds = 1.8f;
        public const int LevelPx = 16;
        private static readonly (int X0, int Y0, int X1, int Y1) AnchorBlock = (0, 0, 2, 2);

        public static readonly Dictionary<string, string> BiomeNames = new()
        {
            ["home"] = "Desa Sekar", ["meadow"] = "Padang Rumput", ["forest"] = "Hutan Kecil", ["pond"] = "Telaga",
            ["rocky"] = "Bukit Batu", ["fields"] = "Lahan Terbuka", ["falls"] = "Air Terjun Lentera",
        };

        private static readonly Dictionary<(int, int), string> ForcedBiomes = new()
        {
            [(3, 1)] = "fields", [(1, 3)] = "fields", [(-1, 1)] = "fields", [(1, -1)] = "meadow", [(3, 3)] = "pond",
            [(-1, -1)] = "forest", [(3, -1)] = "rocky", [(0, -1)] = "rocky", [(-2, 1)] = "falls",
        };

        private readonly Dictionary<string, ObjectDef> defs;

        public ChunkWorld(long seed, Dictionary<string, ObjectDef> defs, TileMap anchor)
        {
            Seed = seed;
            this.defs = defs;
            Name = anchor.Name;
            Spawn = anchor.Spawn;
            Entities = new List<JsonObject>(anchor.Entities);
            var rect = (0, 0, anchor.Width - 1, anchor.Height - 1);
            for (int cy = AnchorBlock.Y0; cy <= AnchorBlock.Y1; cy++)
                for (int cx = AnchorBlock.X0; cx <= AnchorBlock.X1; cx++)
                {
                    var gen = Generate(seed, cx, cy, "home", rect);
                    var rows = gen.Ground.Select(r => r.ToCharArray()).ToArray();
                    for (int ly = 0; ly < Size; ly++)
                        for (int lx = 0; lx < Size; lx++)
                        {
                            int x = cx * Size + lx, y = cy * Size + ly;
                            if (x < anchor.Width && y < anchor.Height) rows[ly][lx] = anchor.Ground[y][x];
                        }
                    var objs = gen.Objects.Select(o => new PlacedObject(defs[o.Type], o.X, o.Y)).ToList();
                    objs.AddRange(anchor.Objects.Where(o => o.X / Size == cx && o.Y / Size == cy));
                    Add(new Chunk(cx, cy, "home", rows.Select(r => new string(r)).ToArray(), objs, gen.Entities, anchor: true));
                }
        }

        public long Seed { get; }
        public string Name { get; }
        public (int X, int Y) Spawn { get; }
        public List<JsonObject> Entities { get; }
        public Dictionary<(int, int), Chunk> Chunks { get; } = new();

        public static long MixSeed(long seed, int cx, int cy, int salt = 0)
            => (seed * 2654435761L ^ cx * 73856093L ^ cy * 19349663L ^ salt * 83492791L) & 0xFFFFFFFFL;

        public static int RingOf(int cx, int cy)
        {
            int dx = Math.Max(Math.Max(AnchorBlock.X0 - cx, 0), cx - AnchorBlock.X1);
            int dy = Math.Max(Math.Max(AnchorBlock.Y0 - cy, 0), cy - AnchorBlock.Y1);
            return Math.Max(dx, dy);
        }

        public static string BiomeFor(long seed, int cx, int cy)
        {
            if (RingOf(cx, cy) == 0) return "home";
            if (ForcedBiomes.TryGetValue((cx, cy), out var b)) return b;
            double r = new Rng(MixSeed(seed, cx, cy, 1)).Random();
            foreach (var (biome, upto) in new[] { ("meadow", 0.28), ("forest", 0.52), ("fields", 0.64), ("pond", 0.74), ("falls", 0.82), ("rocky", 1.0) })
                if (r < upto) return biome;
            return "meadow";
        }

        // -------------------------------------------------------------- generator
        public static GenChunk Generate(long seed, int cx, int cy, string? biome = null, (int, int, int, int)? anchorRect = null)
        {
            biome ??= BiomeFor(seed, cx, cy);
            var rng = new Rng(MixSeed(seed, cx, cy));
            int ox = cx * Size, oy = cy * Size;
            var g = new char[Size, Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) g[y, x] = 'g';
            var hgt = new int[Size, Size];
            var ramps = new Dictionary<(int, int), (int, int)>();
            var reserved = new HashSet<(int, int)>();
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) if (x is 7 or 8 || y is 7 or 8) reserved.Add((x, y));
            var placed = new List<(string, int, int)>();
            var taken = new HashSet<(int, int)>();
            var entities = new List<JsonObject>();

            void Scatter(string[] kinds, int count, int gap = 2, bool blocking = true, string on = "g", char near = '\0')
            {
                int done = 0, tries = 0;
                while (done < count && tries < 400)
                {
                    tries++;
                    int x = rng.RandInt(0, Size - 1), y = rng.RandInt(0, Size - 1);
                    if (!on.Contains(g[y, x]) || taken.Contains((x, y)) || ramps.ContainsKey((x, y))) continue;
                    if (near == '\0' && on != "w" && reserved.Contains((x, y))) continue;
                    if (near != '\0' && !new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d =>
                            x + d.Item1 >= 0 && x + d.Item1 < Size && y + d.Item2 >= 0 && y + d.Item2 < Size && g[y + d.Item2, x + d.Item1] == near))
                        continue;
                    if (blocking && taken.Any(t => Math.Max(Math.Abs(x - t.Item1), Math.Abs(y - t.Item2)) < gap)) continue;
                    placed.Add((rng.Choice(kinds), ox + x, oy + y));
                    if (blocking) taken.Add((x, y));
                    done++;
                }
            }

            void Stair((int, int)[] tiles, (int, int) face)
            {
                foreach (var (x, y) in tiles)
                {
                    hgt[y, x] = Math.Max(hgt[y, x], 1);
                    ramps[(x, y)] = face;
                    reserved.Add((x, y));
                    reserved.Add((x + face.Item1, y + face.Item2));
                }
            }

            List<(int X, int Y)> Plateau((int, int) q, bool top = true, char rampSide = '\0')
            {
                var (qx, qy) = q;
                var cut = new HashSet<(int, int)>();
                foreach (var (a, b) in new[] { (4, 0), (0, 4) }) if (rng.Random() < 0.6) cut.Add((qx + 1 + a, qy + 1 + b));
                for (int y = qy + 1; y < qy + 6; y++)
                    for (int x = qx + 1; x < qx + 6; x++)
                        if (!cut.Contains((x, y))) hgt[y, x] = 1;
                for (int x = qx; x < qx + 7; x++)
                    for (int y = qy; y < qy + 7; y++)
                        if (x == qx || x == qx + 6 || y == qy || y == qy + 6) reserved.Add((x, y));
                char side = rampSide != '\0' ? rampSide : rng.Choice(new[] { 'l', 'r' });
                int k = rng.RandInt(2, 3);
                if (side == 'l') Stair(new[] { (qx + k, qy + 5), (qx + k + 1, qy + 5) }, (0, 1));
                else Stair(new[] { (qx + 5, qy + k), (qx + 5, qy + k + 1) }, (1, 0));
                var result = new List<(int, int)>();
                if (!top)
                {
                    for (int y = qy + 1; y < qy + 6; y++) for (int x = qx + 1; x < qx + 6; x++) if (hgt[y, x] == 1) result.Add((x, y));
                    return result;
                }
                for (int y = qy + 1; y < qy + 4; y++) for (int x = qx + 1; x < qx + 4; x++) hgt[y, x] = 2;
                if (side == 'r') Stair(new[] { (qx + 2, qy + 3), (qx + 3, qy + 3) }, (0, 1));
                else Stair(new[] { (qx + 3, qy + 1), (qx + 3, qy + 2) }, (1, 0));
                for (int y = qy + 1; y < qy + 4; y++) for (int x = qx + 1; x < qx + 4; x++) result.Add((x, y));
                return result;
            }

            void Critters(string kind, int x, int y, int count, float radius = 3f)
                => entities.Add(new JsonObject { ["type"] = "critter", ["kind"] = kind, ["x"] = ox + x, ["y"] = oy + y, ["count"] = count, ["radius"] = radius });

            void Landmark(string kind, int x, int y, string? script = null, string prompt = "Periksa", string? ident = null)
            {
                placed.Add((kind, ox + x, oy + y));
                taken.Add((x, y));
                if (script != null)
                    entities.Add(new JsonObject { ["type"] = "interact", ["id"] = ident ?? $"{kind}_{cx}_{cy}", ["x"] = ox + x, ["y"] = oy + y, ["script"] = script, ["prompt"] = prompt });
            }

            var flowers = new[] { "flower_red", "flower_yellow", "flower_blue", "flower_white" };
            int pcx = 0, pcy = 0;
            if (biome == "pond")
            {
                int qx = rng.RandInt(0, 1), qy = rng.RandInt(0, 1);
                pcx = 3 + 9 * qx; pcy = 3 + 9 * qy;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        double d = Math.Pow((x - pcx) / 2.7, 2) + Math.Pow((y - pcy) / 2.3, 2);
                        if (d <= 1) g[y, x] = 'w';
                        else if (Math.Pow((x - pcx) / 4.1, 2) + Math.Pow((y - pcy) / 3.7, 2) <= 1) g[y, x] = 's';
                    }
            }
            else if (biome == "fields")
            {
                foreach (var (x0, y0) in new[] { (9, 1), (1, 10) })
                    for (int y = y0; y < y0 + 5; y++) for (int x = x0; x < x0 + 6; x++) g[y, x] = 'f';
            }
            if (biome != "home")
                for (int i = 0; i < Size; i++) { g[7, i] = 'd'; g[i, 7] = 'd'; }

            if (biome == "rocky")
            {
                var quads = new List<(int, int)> { (0, 0), (9, 0), (0, 9), (9, 9) };
                rng.Shuffle(quads);
                var peak = Plateau(quads[0], true).Where(t => !ramps.ContainsKey(t) && !reserved.Contains(t)).ToList();
                if (peak.Count > 0)
                {
                    var (bx, by) = peak.OrderBy(t => t.X + t.Y).First();
                    Landmark("bench", bx, by, "@sit", "Duduk");
                }
                var low = Plateau(quads[1], false).Where(t => !ramps.ContainsKey(t) && !reserved.Contains(t)).ToList();
                if (low.Count > 0)
                {
                    var (mx, my) = low.OrderBy(t => t.X + t.Y).First();
                    Landmark("menhir", mx, my);
                }
            }
            else if (biome == "falls")
            {
                Plateau((0, 0), true, 'r');
                for (int y = 1; y < 6; y++) { g[y, 1] = 'w'; hgt[y, 1] = y <= 3 ? 2 : 1; }
                for (int y = 6; y < 11; y++)
                    for (int x = 0; x < 6; x++)
                    {
                        double d = Math.Pow((x - 1.6) / 2.6, 2) + Math.Pow((y - 7.6) / 2.4, 2);
                        if (d <= 1 || (x == 1 && y == 6)) g[y, x] = y is 7 or 8 ? 'b' : 'w';
                        else if (d <= 1.9 && g[y, x] == 'g') g[y, x] = 's';
                    }
                Landmark("lantern", 5, 5, "lentera", "Periksa", $"lentera_{cx}_{cy}");
                Landmark("bench", 3, 1, "@sit", "Duduk");
                for (int x = 2; x < 6; x++) for (int y = 1; y < 6; y++) reserved.Add((x, y));
            }

            switch (biome)
            {
                case "meadow":
                    if (rng.Random() < 0.4)
                    {
                        foreach (var (a, b) in new[] { (10, 3), (12, 2), (14, 3), (10, 5), (14, 5), (12, 6) }) Landmark("menhir", a, b);
                        entities.Add(new JsonObject { ["type"] = "interact", ["id"] = $"stones_{cx}_{cy}", ["x"] = ox + 12, ["y"] = oy + 4, ["script"] = "stones", ["prompt"] = "Periksa" });
                        for (int a = 10; a < 15; a++) for (int b = 2; b < 7; b++) reserved.Add((a, b));
                    }
                    if (rng.Random() < 0.45)
                    {
                        Landmark("campfire", 3, 12, "@sit", "Duduk di dekat api", $"camp_{cx}_{cy}");
                        Landmark("tent", 2, 10);
                        Landmark("log", 5, 13);
                        for (int a = 1; a < 7; a++) for (int b = 9; b < 15; b++) reserved.Add((a, b));
                    }
                    Scatter(new[] { "tree_oak", "tree_birch" }, rng.RandInt(3, 6), 3);
                    Scatter(new[] { "bush", "bush_berry" }, rng.RandInt(2, 4));
                    Scatter(flowers, rng.RandInt(14, 22), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(10, 16), blocking: false);
                    Scatter(new[] { "tree_sakura", "tree_maple", "tree_fruit" }, rng.RandInt(1, 3), 3);
                    Scatter(new[] { "tall_grass_0", "tall_grass_1" }, rng.RandInt(14, 22), blocking: false);
                    Critters("butterfly", 4, 4, rng.RandInt(2, 4));
                    Critters("butterfly", 12, 11, rng.RandInt(1, 3));
                    Critters("bird", 10, 5, rng.RandInt(1, 3), 4f);
                    break;
                case "forest":
                    if ((cx, cy) == (-1, -1) || rng.Random() < 0.3)
                    {
                        Landmark("tree_giant", 11, 11, "tree_giant", "Periksa", $"giant_{cx}_{cy}");
                        for (int a = 9; a < 14; a++) for (int b = 10; b < 14; b++) reserved.Add((a, b));
                    }
                    Scatter(new[] { "tree_oak", "tree_pine", "tree_birch", "tree_pine" }, rng.RandInt(18, 24), 2);
                    Scatter(new[] { "bush", "bush_berry" }, rng.RandInt(4, 6));
                    Scatter(new[] { "stump" }, rng.RandInt(1, 3), 3);
                    Scatter(flowers, rng.RandInt(3, 6), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(6, 10), blocking: false);
                    Scatter(new[] { "log", "tree_dead", "rock_moss" }, rng.RandInt(2, 4), 2);
                    Scatter(new[] { "mushroom", "fern", "fern" }, rng.RandInt(12, 18), blocking: false);
                    Critters("bird", 5, 9, rng.RandInt(2, 3), 4f);
                    Critters("bird", 11, 4, rng.RandInt(1, 2), 3f);
                    break;
                case "pond":
                    Scatter(new[] { "tree_oak", "tree_birch" }, rng.RandInt(2, 4), 3);
                    Scatter(new[] { "rock" }, rng.RandInt(1, 3));
                    Scatter(flowers, rng.RandInt(8, 14), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(6, 10), blocking: false);
                    Scatter(new[] { "tree_palm", "tree_willow" }, rng.RandInt(2, 4), 3, on: "gs");
                    Scatter(new[] { "reeds" }, rng.RandInt(8, 12), blocking: false, on: "s", near: 'w');
                    Scatter(new[] { "lilypad_0", "lilypad_1" }, rng.RandInt(3, 5), blocking: false, on: "w");
                    Scatter(new[] { "tall_grass_0", "tall_grass_1" }, rng.RandInt(6, 10), blocking: false);
                    Critters("duck", pcx, pcy, rng.RandInt(2, 3), 2f);
                    Critters("butterfly", 8, 8, rng.RandInt(1, 3));
                    break;
                case "rocky":
                    Scatter(new[] { "rock", "rock_big", "rock" }, rng.RandInt(7, 10), 2);
                    Scatter(new[] { "bush" }, rng.RandInt(2, 3));
                    Scatter(new[] { "tree_pine" }, rng.RandInt(3, 5), 3);
                    Scatter(flowers, rng.RandInt(4, 8), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(8, 12), blocking: false);
                    Scatter(new[] { "ruin", "rock_moss", "tree_dead" }, rng.RandInt(2, 4), 2);
                    Scatter(new[] { "fern", "tall_grass_1" }, rng.RandInt(6, 10), blocking: false);
                    Critters("bird", 8, 12, rng.RandInt(1, 2), 3f);
                    break;
                case "falls":
                    Scatter(new[] { "tree_pine", "tree_birch" }, rng.RandInt(5, 8), 2);
                    Scatter(new[] { "rock", "rock_big" }, rng.RandInt(3, 5), 2);
                    Scatter(new[] { "bush" }, rng.RandInt(2, 3));
                    Scatter(flowers, rng.RandInt(8, 12), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(8, 12), blocking: false);
                    Scatter(new[] { "bamboo", "tree_willow" }, rng.RandInt(3, 5), 2);
                    Scatter(new[] { "rock_moss" }, rng.RandInt(1, 2), 2);
                    Scatter(new[] { "fern", "mushroom", "tall_grass_0" }, rng.RandInt(10, 16), blocking: false);
                    Critters("butterfly", 9, 11, rng.RandInt(2, 3));
                    Critters("duck", 2, 9, 2, 1.5f);
                    break;
                case "fields":
                    placed.Add(("scarecrow", ox + 9, oy + 6));
                    taken.Add((9, 6));
                    Scatter(new[] { "tree_oak" }, rng.RandInt(2, 3), 3);
                    Scatter(new[] { "rock" }, rng.RandInt(1, 2));
                    Scatter(flowers, rng.RandInt(8, 12), blocking: false);
                    Scatter(new[] { "grass_tuft" }, rng.RandInt(8, 12), blocking: false);
                    Scatter(new[] { "haystack", "hay_bale", "tree_banana" }, rng.RandInt(2, 4), 2);
                    Scatter(new[] { "tall_grass_0", "tall_grass_1" }, rng.RandInt(8, 14), blocking: false);
                    Critters("bird", 4, 4, rng.RandInt(2, 3), 3f);
                    break;
                case "home":
                    Scatter(new[] { "tree_oak", "tree_birch" }, rng.RandInt(2, 4), 3);
                    Scatter(flowers, rng.RandInt(8, 12), blocking: false);
                    break;
            }
            if (biome == "meadow" && rng.Random() < 0.5)
                entities.Add(new JsonObject { ["type"] = "critter", ["x"] = ox + 11, ["y"] = oy + 11, ["count"] = rng.RandInt(1, 3), ["radius"] = 3f });

            var rows = new string[Size];
            for (int y = 0; y < Size; y++) { var row = new char[Size]; for (int x = 0; x < Size; x++) row[x] = g[y, x]; rows[y] = new string(row); }
            if (anchorRect is var (ax0, ay0, ax1, ay1))
            {
                bool Inside(int x, int y) => ax0 <= x && x <= ax1 && ay0 <= y && y <= ay1;
                placed = placed.Where(o => !Inside(o.Item2, o.Item3)).ToList();
                entities = entities.Where(e => !Inside(J.I(e["x"]), J.I(e["y"]))).ToList();
            }
            return new GenChunk { Cx = cx, Cy = cy, Biome = biome, Ground = rows, Objects = placed, Entities = entities, Height = hgt, Ramps = ramps };
        }

        // -------------------------------------------------------------- world
        private void Add(Chunk ch)
        {
            Chunks[(ch.Cx, ch.Cy)] = ch;
            foreach (var o in ch.Objects) if (o.Def.Blocking) foreach (var t in o.Tiles()) Blockers.Add(t);
        }

        public Chunk Ensure(int cx, int cy)
        {
            if (Chunks.TryGetValue((cx, cy), out var ch)) return ch;
            var gen = Generate(Seed, cx, cy);
            var objs = gen.Objects.Where(o => defs.ContainsKey(o.Type))
                .Select(o => new PlacedObject(defs[o.Type], o.X, o.Y, gen.Height[MathX.Mod(o.Y, Size), MathX.Mod(o.X, Size)] * LevelPx)).ToList();
            ch = new Chunk(cx, cy, gen.Biome, gen.Ground, objs, gen.Entities, height: gen.Height, ramps: gen.Ramps);
            Add(ch);
            return ch;
        }

        public Chunk? ChunkAt(int x, int y) => Chunks.GetValueOrDefault((MathX.FloorDiv(x, Size), MathX.FloorDiv(y, Size)));

        public override string? KindAt(int x, int y)
        {
            var ch = ChunkAt(x, y);
            if (ch == null || ch.State == ChunkState.Hidden) return null;
            return GroundLegend[ch.Ground[MathX.Mod(y, Size)][MathX.Mod(x, Size)]];
        }

        public char GroundChar(int x, int y)
        {
            var ch = ChunkAt(x, y);
            return ch == null || ch.State == ChunkState.Hidden ? '\0' : ch.Ground[MathX.Mod(y, Size)][MathX.Mod(x, Size)];
        }

        public bool IsVisible(int cx, int cy) => Chunks.TryGetValue((cx, cy), out var ch) && ch.State != ChunkState.Hidden;

        public override int HeightAt(int x, int y)
        {
            var ch = ChunkAt(x, y);
            return ch != null && ch.State != ChunkState.Hidden ? ch.Height[MathX.Mod(y, Size), MathX.Mod(x, Size)] : 0;
        }

        public (int, int)? RampDir(int x, int y)
        {
            var ch = ChunkAt(x, y);
            return ch != null && ch.Ramps.TryGetValue((MathX.Mod(x, Size), MathX.Mod(y, Size)), out var d) ? d : null;
        }

        /// <summary>Bisa melangkah dari a ke b? Selevel: selalu. Beda satu level: hanya lewat tangga yang menghadap tile rendah.</summary>
        public override bool Passable((int, int) a, (int, int) b)
        {
            if (a == b) return true;
            int ha = HeightAt(a.Item1, a.Item2), hb = HeightAt(b.Item1, b.Item2);
            if (ha == hb) return true;
            var (hi, lo) = ha > hb ? (a, b) : (b, a);
            return Math.Abs(ha - hb) == 1 && RampDir(hi.Item1, hi.Item2) == (lo.Item1 - hi.Item1, lo.Item2 - hi.Item2);
        }

        public Chunk Reveal(int cx, int cy, bool animate = true)
        {
            var ch = Ensure(cx, cy);
            if (ch.State != ChunkState.Hidden) return ch;
            ch.State = animate ? ChunkState.Revealing : ChunkState.Revealed;
            ch.T = 0;
            DirtyAround(ch);
            return ch;
        }

        private void DirtyAround(Chunk ch)
        {
            ch.Dirty = true;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (Chunks.TryGetValue((ch.Cx + dx, ch.Cy + dy), out var n)) n.Dirty = true;
        }

        /// <summary>Majukan animasi muncul; mulai memunculkan chunk dekat pemain. Mengembalikan (yang baru muncul, terkunci kabut?).</summary>
        public (List<Chunk> Started, bool Locked) Update(float px, float py, float dt, int ringsOpen = 1)
        {
            var started = new List<Chunk>();
            bool locked = false;
            int x0 = (int)MathF.Floor((px - RevealRadius) / Size), x1 = (int)MathF.Floor((px + RevealRadius) / Size);
            int y0 = (int)MathF.Floor((py - RevealRadius) / Size), y1 = (int)MathF.Floor((py + RevealRadius) / Size);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    var ch = Ensure(cx, cy);
                    if (ch.State != ChunkState.Hidden) continue;
                    if (RingOf(cx, cy) > ringsOpen) { locked = true; continue; }
                    Reveal(cx, cy);
                    started.Add(ch);
                }
            foreach (var ch in Chunks.Values.ToList())
                if (ch.State == ChunkState.Revealing)
                {
                    ch.T += dt;
                    if (ch.T >= RevealSeconds) { ch.State = ChunkState.Revealed; DirtyAround(ch); }
                }
            return (started, locked);
        }

        public JsonObject ToJson()
        {
            var arr = new JsonArray();
            foreach (var c in Chunks.Values.Where(c => c.Visible && !c.Anchor).OrderBy(c => c.Cx).ThenBy(c => c.Cy))
                arr.Add(new JsonArray(c.Cx, c.Cy));
            return new JsonObject { ["seed"] = Seed, ["revealed"] = arr };
        }

        public void LoadJson(JsonNode? data)
        {
            foreach (var p in data?["revealed"]?.AsArray() ?? new JsonArray())
                Reveal(J.I(p![0]), J.I(p[1]), animate: false);
        }
    }
}
