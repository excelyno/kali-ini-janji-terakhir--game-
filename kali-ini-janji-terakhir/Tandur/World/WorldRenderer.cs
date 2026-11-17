using Tandur.Engine;
using Tandur.Entities;

namespace Tandur.World
{
    /// <summary>Kursor tile: posisi + warna (ok / bad / info).</summary>
    public readonly record struct TileCursor(int X, int Y, Preview Color);

    /// <summary>
    /// Menggambar ChunkWorld: satu mesh tanah per chunk, animasi "naik" untuk chunk baru, dan sprite
    /// yang diurutkan kedalaman tiap frame (blok bukit, objek, tanaman, karakter).
    /// </summary>
    public sealed class WorldRenderer
    {
        private const int GrassVariants = 4, PathVariants = 3, SandVariants = 2, FarmVariants = 2, WaterFrames = 4;
        private const float RisePixels = 44, ObjectRise = 70, TileSpan = 0.75f, TileTime = 0.55f, ObjectLag = 0.4f, ObjectTime = 0.5f;
        private const float PadX = 40, PadTop = 120, PadBottom = 140;
        private const int S = ChunkWorld.Size;

        private readonly Atlas atlas;
        private readonly ChunkWorld world;
        private readonly FarmField? farm;
        private readonly Dictionary<Chunk, List<Block>> blockCache = new();
        private readonly List<Item> items = new(4096);

        public WorldRenderer(Atlas atlas, ChunkWorld world, FarmField? farm)
        {
            this.atlas = atlas;
            this.world = world;
            this.farm = farm;
        }

        public bool Dry { get; set; }
        public bool LanternsLit { get; set; }
        public float Wind { get; set; } = 1f;
        public float Night { get; set; }
        public float Daylight { get; set; } = 1f;
        public List<(float X, float Y)> Foam { get; } = new();

        private sealed record Block(int X, int Y, int Lx, int Ly, int H, string Kind, int DropL, int DropR, string FaceL, string FaceR);

        private enum ItemKind { Block, Sprite, Entity }

        private readonly struct Item
        {
            public readonly float Depth; public readonly int Order; public readonly ItemKind Kind;
            public readonly Block? Blk; public readonly string? Name; public readonly float X, Y, A, Shear; public readonly Actor? Actor;
            public Item(float depth, int order, ItemKind kind, Block? blk = null, string? name = null, float x = 0, float y = 0, float a = 1, float shear = 0, Actor? actor = null)
            {
                Depth = depth; Order = order; Kind = kind; Blk = blk; Name = name; X = x; Y = y; A = a; Shear = shear; Actor = actor;
            }
        }

        public static long Hash(int x, int y) => (x * 73856093L) ^ (y * 19349663L);

        private float Sway(float amount, int x, int y, float t)
        {
            float phase = x * 0.37f + y * 0.23f;
            return amount * Wind * (0.7f * MathF.Sin(t * 1.3f + phase) + 0.3f * MathF.Sin(t * 2.9f + phase * 1.7f));
        }

        private string ObjectSprite(ObjectDef d, float t, int x, int y)
        {
            if (d.Frames > 1)
                return $"{d.Sprite}_{(int)(t * d.Fps * (d.Sprite == "windmill" ? Wind : 1f) + (x * 3 + y)) % d.Frames}";
            if (d.Sprite == "lantern_off" && LanternsLit) return "lantern_on";
            if (d.Lit.Length > 0 && Night > 0.25f) return d.Lit;
            return d.Sprite;
        }

        public static string TileSprite(string kind, int x, int y)
        {
            long h = Hash(x, y) & 0x7FFFFFFF;
            switch (kind)
            {
                case "grass": { long v = h % 23; return $"tile_grass_{(v < 6 ? v : v % GrassVariants)}"; }
                case "stone": return $"tile_stone_{h % 2}";
                case "paddy": return $"tile_paddy_{h % 2}";
                case "path": return $"tile_path_{h % PathVariants}";
                case "sand": return $"tile_sand_{h % SandVariants}";
                case "farmland": return $"tile_farmland_{h % FarmVariants}";
                case "planks": return "tile_planks";
                default: return "tile_water_0";
            }
        }

        // -------------------------------------------------------------- baking
        private void Bake(Chunk ch, SpriteBatch batch)
        {
            batch.Flush();
            ch.Water.Clear();
            int ox = ch.Cx * S, oy = ch.Cy * S;
            for (int depth = 0; depth < 2 * S - 1; depth++)
                for (int ly = 0; ly < S; ly++)
                {
                    int lx = depth - ly;
                    if (lx < 0 || lx >= S || ch.Height[ly, lx] != 0) continue;
                    int x = ox + lx, y = oy + ly;
                    string kind = WorldMap.GroundLegend[ch.Ground[ly][lx]];
                    var (wx, wy) = Iso.ToWorld(x, y);
                    if (kind == "water")
                    {
                        ch.Water.Add((wx, wy, (int)(Hash(x, y) & 3)));
                        batch.Sprite(atlas["tile_water_0"], wx, wy);
                        continue;
                    }
                    batch.Sprite(atlas[TileSprite(kind, x, y)], wx, wy);
                }
            if (!Settled(ch.Cx, ch.Cy + 1))
                for (int lx = 0; lx < S; lx++) { var (wx, wy) = Iso.ToWorld(ox + lx, oy + S - 1); batch.Sprite(atlas["cliff_l"], wx, wy); }
            if (!Settled(ch.Cx + 1, ch.Cy))
                for (int ly = 0; ly < S; ly++) { var (wx, wy) = Iso.ToWorld(ox + S - 1, oy + ly); batch.Sprite(atlas["cliff_r"], wx, wy); }
            ch.Mesh?.Dispose();
            ch.Mesh = batch.Freeze();
            ch.Dirty = false;
        }

        private bool Settled(int cx, int cy) => world.Chunks.TryGetValue((cx, cy), out var c) && c.State == ChunkState.Revealed;

        // -------------------------------------------------------------- raised terrain
        private List<Block> Blocks(Chunk ch)
        {
            if (blockCache.TryGetValue(ch, out var cached)) return cached;
            var outList = new List<Block>();
            int ox = ch.Cx * S, oy = ch.Cy * S;
            int Level(int lx, int ly) => lx >= 0 && lx < S && ly >= 0 && ly < S ? ch.Height[ly, lx] : 0;
            char Ground(int lx, int ly) => lx >= 0 && lx < S && ly >= 0 && ly < S ? ch.Ground[ly][lx] : 'g';
            for (int ly = 0; ly < S; ly++)
                for (int lx = 0; lx < S; lx++)
                {
                    int h = ch.Height[ly, lx];
                    if (h == 0) continue;
                    string kind = WorldMap.GroundLegend[ch.Ground[ly][lx]];
                    var faces = new List<(int Drop, string Face)>();
                    foreach (var (side, nx, ny) in new[] { ('l', lx, ly + 1), ('r', lx + 1, ly) })
                    {
                        int drop = Math.Max(0, h - Level(nx, ny));
                        string face = "wall";
                        if (drop > 0 && kind == "water" && "wb".Contains(Ground(nx, ny))) face = "fall";
                        else if (drop > 0 && ch.Ramps.TryGetValue((lx, ly), out var r) && r == (side == 'l' ? (0, 1) : (1, 0))) face = "stairs";
                        faces.Add((drop, face));
                    }
                    outList.Add(new Block(ox + lx, oy + ly, lx, ly, h, kind, faces[0].Drop, faces[1].Drop, faces[0].Face, faces[1].Face));
                }
            blockCache[ch] = outList;
            return outList;
        }

        private void DrawBlock(SpriteBatch batch, Block blk, int frame, float alpha, float rise)
        {
            var (wx, wy) = Iso.ToWorld(blk.X, blk.Y);
            float top = wy - blk.H * ChunkWorld.LevelPx + rise;
            var col = new Rgba(1, 1, 1, alpha);
            foreach (var (side, drop, face) in new[] { ("l", blk.DropL, blk.FaceL), ("r", blk.DropR, blk.FaceR) })
                for (int j = 0; j < drop; j++)
                {
                    string name = face == "fall" && !Dry ? $"fall_{side}_{(frame + j) % 4}" : face == "stairs" && j == 0 ? $"stairs_{side}" : $"wall_{side}";
                    batch.Sprite(atlas[name], wx, top + j * ChunkWorld.LevelPx, col);
                }
            string tile = blk.Kind == "water" ? (Dry ? "tile_dry" : $"tile_water_{(frame + (Hash(blk.X, blk.Y) & 3)) % WaterFrames}") : TileSprite(blk.Kind, blk.X, blk.Y);
            float lit = 1f + 0.05f * blk.H;
            batch.Sprite(atlas[tile], wx, top, new Rgba(lit, lit, lit * 0.97f, alpha));
        }

        private static float TileProgress(Chunk ch, int lx, int ly)
        {
            float delay = (2 * S - 2 - (lx + ly)) / (float)(2 * S - 2) * TileSpan;
            return Ease.Clamp01((ch.T - delay) / TileTime);
        }

        private void DrawRevealing(Chunk ch, SpriteBatch batch)
        {
            int ox = ch.Cx * S, oy = ch.Cy * S;
            for (int depth = 0; depth < 2 * S - 1; depth++)
                for (int ly = 0; ly < S; ly++)
                {
                    int lx = depth - ly;
                    if (lx < 0 || lx >= S || ch.Height[ly, lx] != 0) continue;
                    float p = TileProgress(ch, lx, ly);
                    if (p <= 0) continue;
                    int x = ox + lx, y = oy + ly;
                    string kind = WorldMap.GroundLegend[ch.Ground[ly][lx]];
                    var (wx, wy) = Iso.ToWorld(x, y);
                    batch.Sprite(atlas[TileSprite(kind, x, y)], wx, wy + (1 - Ease.OutCubic(p)) * RisePixels, new Rgba(1, 1, 1, Ease.Clamp01(p * 2.2f)));
                }
        }

        // -------------------------------------------------------------- per frame
        public void Draw(Renderer gfx, Camera camera, float t, IEnumerable<Actor> entities, Rgba tint, TileCursor? cursor, Particles? particles, Atmosphere? atmo)
        {
            var (cx, cy) = gfx.ViewWorld(camera, tint);
            var (vw, vh) = gfx.WorldViewSize;
            float left = cx - vw / 2, right = cx + vw / 2, top = cy - vh / 2, bottom = cy + vh / 2;
            var batch = gfx.Batch;
            var chunks = new List<Chunk>();
            foreach (var ch in world.Chunks.Values)
            {
                if (!ch.Visible) continue;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var (a, b) in new[] { (0, 0), (S, 0), (0, S), (S, S) })
                {
                    var (px, py) = Iso.ToWorld(ch.Cx * S + a, ch.Cy * S + b);
                    minX = Math.Min(minX, px); maxX = Math.Max(maxX, px); minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
                }
                if (maxX + PadX < left || minX - PadX > right || maxY + PadBottom < top || minY - PadTop > bottom) continue;
                chunks.Add(ch);
            }
            chunks.Sort((a, b) => (a.Cx + a.Cy, a.Cx).CompareTo((b.Cx + b.Cy, b.Cx)));
            int frame = (int)(t * 2f), fallFrame = (int)(t * 7f);
            items.Clear();
            Foam.Clear();
            foreach (var ch in chunks)
            {
                bool settled = ch.State == ChunkState.Revealed;
                if (settled)
                {
                    if (ch.Dirty || ch.Mesh == null) Bake(ch, batch);
                    gfx.DrawMesh(ch.Mesh!);
                    bool riverDry = Dry && ch.Biome is "falls" or "pond";
                    foreach (var (wx, wy, phase) in ch.Water)
                        if (left - 16 < wx && wx < right + 16 && top - 16 < wy && wy < bottom + 16)
                        {
                            string name = riverDry && phase != 0 ? "tile_dry" : $"tile_water_{(frame + phase) % WaterFrames}";
                            batch.Sprite(atlas[name], wx, wy);
                            if (!riverDry) DrawGlint(batch, wx, wy, t);
                        }
                }
                else DrawRevealing(ch, batch);

                if (ch.Raised)
                    foreach (var blk in Blocks(ch))
                    {
                        float alpha = 1, rise = 0;
                        if (!settled)
                        {
                            float p = TileProgress(ch, blk.Lx, blk.Ly);
                            if (p <= 0) continue;
                            alpha = Ease.Clamp01(p * 2.2f);
                            rise = (1 - Ease.OutCubic(p)) * RisePixels;
                        }
                        items.Add(new Item(blk.X + blk.Y, 0, ItemKind.Block, blk, a: alpha, y: rise));
                        if (settled && !Dry && (blk.FaceL == "fall" || blk.FaceR == "fall"))
                        {
                            var (wx, wy) = Iso.ToWorld(blk.X, blk.Y);
                            int side = blk.FaceL == "fall" ? -8 : 8;
                            int drop = blk.FaceL == "fall" ? blk.DropL : blk.DropR;
                            Foam.Add((wx + side, wy + 4 - (blk.H - drop) * ChunkWorld.LevelPx));
                        }
                    }

                foreach (var o in ch.Objects)
                {
                    var (wx, wy) = o.WorldPosition;
                    wy -= o.Lift;
                    var d = o.Def;
                    string name = ObjectSprite(d, t, o.X, o.Y);
                    if (settled)
                    {
                        if (left - 60 < wx && wx < right + 60 && top < wy && wy < bottom + 130)
                        {
                            if (d.Category == "water") wy += MathF.Round(MathF.Sin(t * 1.6f + o.X * 1.3f + o.Y) * 0.8f);
                            float shear = d.Sway != 0 ? Sway(d.Sway, o.X, o.Y, t) : 0;
                            items.Add(new Item(o.Depth, 1, ItemKind.Sprite, name: name, x: wx, y: wy, shear: shear));
                        }
                        continue;
                    }
                    var (ocx, ocy) = o.Center;
                    int lx = MathX.Round(ocx) - ch.Cx * S, ly = MathX.Round(ocy) - ch.Cy * S;
                    float delay = (2 * S - 2 - (lx + ly)) / (float)(2 * S - 2) * TileSpan + ObjectLag;
                    float pp = Ease.Clamp01((ch.T - delay) / ObjectTime);
                    if (pp > 0) items.Add(new Item(o.Depth, 1, ItemKind.Sprite, name: name, x: wx, y: wy + (1 - Ease.OutBack(pp)) * ObjectRise, a: Ease.Clamp01(pp * 2)));
                }
            }

            if (farm != null)
                foreach (var ((x, y), plot) in farm.Plots)
                {
                    var (wx, wy) = Iso.ToWorld(x, y);
                    if (!(left - 16 < wx && wx < right + 16 && top - 16 < wy && wy < bottom + 16)) continue;
                    batch.Sprite(atlas[plot.Watered ? "tile_tilled_wet" : "tile_tilled"], wx, wy);
                    if (plot.Crop != null)
                    {
                        items.Add(new Item(x + y, 1, ItemKind.Sprite, name: $"crop_{plot.Crop}_{plot.Stage}", x: wx, y: wy, shear: plot.Stage > 0 ? Sway(0.7f, x, y, t) : 0));
                        if (farm.IsRipe(x, y) && ((int)(t * 1.3f + x * 0.7f + y * 0.3f) % 5 == 0))
                            items.Add(new Item(x + y + 0.01f, 1, ItemKind.Sprite, name: $"sparkle_{(int)(t * 8) % 4}", x: wx + 4, y: wy - 16, a: 0.9f));
                    }
                }

            foreach (var e in entities)
            {
                float depth = e.Depth;
                int tx = MathX.Round(e.GX), ty = MathX.Round(e.GY);
                if (world.HeightAt(tx, ty) != 0) depth = Math.Max(depth, tx + ty);
                items.Add(new Item(depth, 2, ItemKind.Entity, actor: e));
            }
            var order = Enumerable.Range(0, items.Count).ToArray();
            order = order.OrderBy(i => items[i].Depth).ThenBy(i => items[i].Order).ToArray();   // stabil, seperti sort Python

            (float Depth, float X, float Y)? cursorBlock = null;
            if (cursor is { } c)
            {
                float lift = world.HeightAt(c.X, c.Y) * ChunkWorld.LevelPx;
                var (wx, wy) = Iso.ToWorld(c.X, c.Y);
                if (lift > 0) cursorBlock = (c.X + c.Y, wx, wy - lift);
                else batch.Sprite(atlas[CursorSprite(c.Color)], wx, wy);
            }
            foreach (int idx in order)
            {
                var it = items[idx];
                switch (it.Kind)
                {
                    case ItemKind.Entity: it.Actor!.Draw(batch, atlas); break;
                    case ItemKind.Sprite: batch.Sprite(atlas[it.Name!], it.X, it.Y, new Rgba(1, 1, 1, it.A), shear: it.Shear); break;
                    default:
                        DrawBlock(batch, it.Blk!, fallFrame, it.A, it.Y);
                        if (cursorBlock is { } cb && cursor is { } cc && it.Depth == cb.Depth && it.Blk!.X == cc.X && it.Blk.Y == cc.Y)
                            batch.Sprite(atlas[CursorSprite(cc.Color)], cb.X, cb.Y);
                        break;
                }
            }
            particles?.Draw(batch);
            atmo?.DrawWorld(gfx, camera, (left, right, top, bottom));
        }

        /// <summary>Kilau matahari di permukaan air (efek tambahan versi C#).</summary>
        private void DrawGlint(SpriteBatch batch, float wx, float wy, float t)
        {
            if (Daylight < 0.3f) return;
            long h = Hash((int)wx, (int)wy) & 0xFFFF;
            float phase = (t * 0.9f + h * 0.013f) % 6f;
            if (phase > 0.5f) return;
            int f = Math.Min(3, (int)(phase / 0.125f));
            float ox = (h % 13) - 6, oy = (h / 13 % 5) - 2;
            batch.Sprite(atlas[$"sparkle_{f}"], MathF.Round(wx + ox), MathF.Round(wy + oy), new Rgba(1, 1, 1, 0.75f * Daylight));
        }

        private static string CursorSprite(Preview p) => p switch { Preview.Ok => "cursor_ok", Preview.Bad => "cursor_bad", _ => "cursor_info" };
    }
}
