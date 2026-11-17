using System.Drawing;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities.Props
{
    /// <summary>
    /// ABSTRAKSI: bangunan yang bisa didatangi dan diajak berinteraksi.
    /// Semua bangunan memakai cara yang sama untuk mengecek jarak (zona di depan bangunan),
    /// tetapi Prompt dan Interact berbeda di tiap subclass (POLIMORFISME).
    /// </summary>
    public abstract class Building : StaticObject, IInteractable
    {
        protected Building(float x, float y, string sprite) : base(x, y, sprite) { }

        /// <summary>Area tempat pemain harus berdiri (atau mengarahkan sasaran) untuk berinteraksi.</summary>
        protected abstract RectangleF InteractZone { get; }

        public virtual bool InRange(FarmWorld world)
            => world.Player.Hitbox.IntersectsWith(InteractZone) || InteractZone.Contains(world.Player.TargetPoint);

        public abstract string Prompt(FarmWorld world);
        public abstract void Interact(FarmWorld world);
    }

    /// <summary>Rumah pemain: pintu di tengah bawah. Masuk = tidur.</summary>
    public sealed class House : Building
    {
        public House(float x, float y) : base(x, y, "house") { }

        // Tembok padat setinggi 2 tile dari dasar; atap di atasnya hanya gambar (bisa "dilewati" di belakang).
        public override RectangleF Hitbox => new(Bounds.X + 10, Y - 100, Bounds.Width - 20, 92);
        protected override RectangleF InteractZone => new(X - 30, Y - 8, 60, 44);

        public override string Prompt(FarmWorld world) => "Masuk rumah (tidur)";

        public override void Interact(FarmWorld world)
            => world.ShowDialogue(new Dialogue("Rumah", "Tidur sampai besok pagi? Permainan akan disimpan.")
                .Choice("Tidur", w => w.Sleep(false))
                .Choice("Nanti saja", _ => { }));
    }

    public sealed class ShopStall : Building
    {
        public ShopStall(float x, float y) : base(x, y, "shop") { }

        public override RectangleF Hitbox => new(Bounds.X + 10, Y - 70, Bounds.Width - 20, 62);
        protected override RectangleF InteractZone => new(Bounds.X + 30, Y - 8, Bounds.Width - 60, 50);

        public override string Prompt(FarmWorld world) => "Buka Toko Bu Lastri";
        public override void Interact(FarmWorld world) => world.OpenShop();
    }

    public sealed class ShippingBin : Building
    {
        public ShippingBin(float x, float y) : base(x, y, "shipping_bin") { }

        public override RectangleF Hitbox => FootBox(48, 24);
        protected override RectangleF InteractZone => RectangleF.Inflate(Hitbox, 30, 30);
        protected override string? ShadowSize => "l";

        public override string Prompt(FarmWorld world)
        {
            int total = world.SellValue();
            return total > 0 ? $"Jual semua hasil (+{total} koin)" : "Peti jual (kosong)";
        }

        public override void Interact(FarmWorld world) => world.SellAll();
    }

    /// <summary>
    /// Satu tile pagar. Sprite dipilih dengan AUTOTILE dari pagar tetangga, jadi pagar
    /// tersambung otomatis (lurus, belok, atau tiang tunggal).
    /// </summary>
    public sealed class Fence : StaticObject
    {
        private readonly int mask;

        public Fence(Point tile, int neighborMask)
            : base(tile.X * TileMap.Tile + TileMap.Tile / 2f, (tile.Y + 1) * TileMap.Tile, $"fence_{neighborMask}")
        {
            Tile = tile;
            mask = neighborMask;
        }

        public Point Tile { get; }

        public override RectangleF Hitbox
        {
            get
            {
                var t = TileMap.TileRect(Tile);
                float left = (mask & TileMap.West) != 0 ? t.Left : X - 8;
                float right = (mask & TileMap.East) != 0 ? t.Right : X + 8;
                float top = (mask & TileMap.North) != 0 ? t.Top : Y - 22;
                float bottom = (mask & TileMap.South) != 0 ? t.Bottom : Y - 6;
                return RectangleF.FromLTRB(left, top, right, bottom);
            }
        }
    }
}
