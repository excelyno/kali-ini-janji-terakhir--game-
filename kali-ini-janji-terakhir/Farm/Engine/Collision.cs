using System.Drawing;

namespace kali_ini_janji_terakhir.Farm.Engine
{
    /// <summary>Apa saja yang punya kotak tabrakan (AABB = axis-aligned bounding box).</summary>
    public interface ICollider
    {
        /// <summary>Kotak tabrakan di dunia. RectangleF.Empty = tidak menghalangi.</summary>
        RectangleF Hitbox { get; }

        /// <summary>Benda diam (pohon, rumah) masuk ke grid spasial; benda bergerak dicek satu per satu.</summary>
        bool IsStatic { get; }
    }

    /// <summary>
    /// BROADPHASE: grid spasial. Setiap collider diam dicatat di semua sel grid yang disentuhnya,
    /// sehingga pencarian tabrakan cukup melihat beberapa sel di sekitar, bukan semua objek di peta.
    /// </summary>
    public sealed class SpatialGrid
    {
        private readonly int cellSize;
        private readonly Dictionary<Point, List<ICollider>> cells = new();

        public SpatialGrid(int cellSize) => this.cellSize = cellSize;

        public void Add(ICollider c)
        {
            foreach (var cell in CellsOf(c.Hitbox))
            {
                if (!cells.TryGetValue(cell, out var list)) cells[cell] = list = new List<ICollider>();
                list.Add(c);
            }
        }

        public void Remove(ICollider c)
        {
            foreach (var cell in CellsOf(c.Hitbox))
                if (cells.TryGetValue(cell, out var list)) list.Remove(c);
        }

        public IEnumerable<ICollider> Query(RectangleF area)
        {
            var seen = new HashSet<ICollider>();
            foreach (var cell in CellsOf(area))
                if (cells.TryGetValue(cell, out var list))
                    foreach (var c in list)
                        if (seen.Add(c)) yield return c;
        }

        private IEnumerable<Point> CellsOf(RectangleF r)
        {
            if (r.IsEmpty) yield break;
            int x0 = (int)MathF.Floor(r.Left / cellSize), x1 = (int)MathF.Floor((r.Right - 0.01f) / cellSize);
            int y0 = (int)MathF.Floor(r.Top / cellSize), y1 = (int)MathF.Floor((r.Bottom - 0.01f) / cellSize);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    yield return new Point(x, y);
        }
    }

    /// <summary>
    /// Dunia tabrakan: tile padat (air, tepi peta) + collider diam (grid spasial) + collider bergerak.
    ///
    /// Gerak memakai teknik "move and slide":
    ///   1. Gerak sumbu X dulu, lalu sumbu Y (dicek terpisah). Kalau X terhalang, Y tetap jalan,
    ///      jadi karakter "menyusur" tembok, tidak menempel.
    ///   2. Gerak dipotong tepat sampai menyentuh penghalang (tanpa celah, tanpa menembus).
    ///   3. CORNER NUDGE: kalau menabrak ujung benda hanya sedikit (beberapa piksel),
    ///      karakter digeser menyamping agar lolos, seperti di Stardew Valley.
    /// </summary>
    public sealed class CollisionWorld
    {
        public const float NudgeRange = 14f;

        private readonly TileMap map;
        private readonly SpatialGrid statics = new(TileMap.Tile);
        private readonly List<ICollider> movers = new();

        public CollisionWorld(TileMap map) => this.map = map;

        public void Add(ICollider c)
        {
            if (c.IsStatic) { if (!c.Hitbox.IsEmpty) statics.Add(c); }
            else movers.Add(c);
        }

        public void Remove(ICollider c)
        {
            if (c.IsStatic) statics.Remove(c);
            else movers.Remove(c);
        }

        /// <summary>Semua kotak padat di sekitar area (tile + benda), kecuali milik "self".</summary>
        public IEnumerable<RectangleF> SolidsNear(RectangleF area, ICollider? self)
        {
            var a = TileMap.TileAt(area.Left, area.Top);
            var b = TileMap.TileAt(area.Right, area.Bottom);
            for (int y = a.Y; y <= b.Y; y++)
                for (int x = a.X; x <= b.X; x++)
                    if (map.IsSolid(new Point(x, y))) yield return TileMap.TileRect(new Point(x, y));

            foreach (var c in statics.Query(area))
                if (!ReferenceEquals(c, self)) yield return c.Hitbox;

            foreach (var c in movers)
                if (!ReferenceEquals(c, self) && !c.Hitbox.IsEmpty && c.Hitbox.IntersectsWith(area)) yield return c.Hitbox;
        }

        public bool IsBlocked(RectangleF box, ICollider? self = null)
            => SolidsNear(box, self).Any(s => s.IntersectsWith(box));

        /// <summary>Gerakkan kotak sejauh (dx, dy) dan kembalikan pergeseran yang benar-benar terjadi.</summary>
        public PointF MoveAndSlide(RectangleF box, float dx, float dy, ICollider? self = null)
        {
            float mx = Sweep(box, dx, horizontal: true, self);
            box.Offset(mx, 0);
            float my = Sweep(box, dy, horizontal: false, self);
            box.Offset(0, my);

            // Corner nudge: terhalang di satu sumbu dan tidak bergerak di sumbu lain.
            if (dx != 0 && MathF.Abs(mx) < MathF.Abs(dx) * 0.5f && dy == 0)
                my += Nudge(box, MathF.Sign(dx), horizontal: true, MathF.Abs(dx), self);
            else if (dy != 0 && MathF.Abs(my) < MathF.Abs(dy) * 0.5f && dx == 0)
                mx += Nudge(box, MathF.Sign(dy), horizontal: false, MathF.Abs(dy), self);
            return new PointF(mx, my);
        }

        /// <summary>Potong gerak satu sumbu sampai menyentuh penghalang terdekat.</summary>
        private float Sweep(RectangleF box, float delta, bool horizontal, ICollider? self)
        {
            if (delta == 0) return 0;
            var moved = box;
            if (horizontal) moved.Offset(delta, 0); else moved.Offset(0, delta);
            var area = RectangleF.Union(box, moved);

            foreach (var s in SolidsNear(area, self))
            {
                if (s.IntersectsWith(box)) continue;          // sudah tumpang tindih: biarkan keluar
                if (!s.IntersectsWith(area)) continue;
                if (horizontal)
                {
                    if (s.Bottom <= box.Top || s.Top >= box.Bottom) continue;
                    delta = delta > 0 ? Math.Min(delta, s.Left - box.Right) : Math.Max(delta, s.Right - box.Left);
                }
                else
                {
                    if (s.Right <= box.Left || s.Left >= box.Right) continue;
                    delta = delta > 0 ? Math.Min(delta, s.Top - box.Bottom) : Math.Max(delta, s.Bottom - box.Top);
                }
            }
            return delta;
        }

        /// <summary>Cari celah di samping (maks NudgeRange piksel); geser ke sana maksimal 'step'.</summary>
        private float Nudge(RectangleF box, int forward, bool horizontal, float step, ICollider? self)
        {
            for (int offset = 1; offset <= NudgeRange; offset++)
                foreach (int side in new[] { -1, 1 })
                {
                    var test = box;
                    if (horizontal) test.Offset(forward, side * offset);
                    else test.Offset(side * offset, forward);
                    var path = box;
                    if (horizontal) path.Offset(0, side * offset); else path.Offset(side * offset, 0);
                    if (!IsBlocked(test, self) && !IsBlocked(path, self))
                        return side * Math.Min(offset, step);
                }
            return 0;
        }
    }
}
