using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Tools
{
    /// <summary>Cangkul: mengubah tanah ladang kosong menjadi petak tanam.</summary>
    public sealed class Hoe : Tool
    {
        public Hoe() : base("Cangkul", 2) { }
        public override string IconName => "tool_hoe";
        public override string? SwingName => "hoe";

        protected override string? Validate(FarmWorld world, Player player, Point tile)
        {
            if (world.PlotAt(tile) != null) return "";
            if (!world.IsFarmTile(tile)) return "Cangkul hanya di tanah ladang.";
            if (!world.IsTileFree(tile)) return "Ada penghalang. Bersihkan dulu.";
            return null;
        }

        protected override void Perform(FarmWorld world, Player player, Point tile)
        {
            world.TillTile(tile);
            var c = TileMap.TileCenter(tile);
            world.Effects.Burst("fx_clod", c.X, c.Y + 8, 6, speed: 90, life: 0.45f);
            world.Raise(GameEventType.Tilled);
        }
    }

    /// <summary>
    /// Penyiram. ENKAPSULASI: isi air privat; hanya berkurang saat menyiram,
    /// penuh lewat Refill(), dan kapasitas naik lewat Upgrade() (sekali saja).
    /// Arahkan ke air (kolam) untuk mengisi ulang, seperti di Stardew Valley.
    /// </summary>
    public sealed class WateringCan : Tool
    {
        public const int BasicCapacity = 12;
        public const int BigCapacity = 30;
        private int water;

        public WateringCan() : base("Penyiram", 1)
        {
            Capacity = BasicCapacity;
            water = Capacity;
        }

        public int Capacity { get; private set; }
        public int Water => water;
        public bool IsUpgraded => Capacity > BasicCapacity;
        public override string IconName => "tool_can";
        public override string? SwingName => "can";
        public override string? Badge(Player player) => water.ToString();

        public void Refill() => water = Capacity;

        public bool Upgrade()
        {
            if (IsUpgraded) return false;
            Capacity = BigCapacity;
            water = Capacity;
            return true;
        }

        public void Restore(int savedWater, int savedCapacity)
        {
            Capacity = savedCapacity >= BigCapacity ? BigCapacity : BasicCapacity;
            water = Math.Clamp(savedWater, 0, Capacity);
        }

        protected override float EnergyCostAt(FarmWorld world, Point tile) => world.IsWater(tile) ? 0 : EnergyCost;

        protected override string? Validate(FarmWorld world, Player player, Point tile)
        {
            if (world.IsWater(tile)) return water < Capacity ? null : "Penyiram sudah penuh.";
            if (water < 1) return "Air habis! Arahkan penyiram ke kolam untuk mengisi.";
            var plot = world.PlotAt(tile);
            return plot == null || plot.IsWatered ? "" : null;
        }

        protected override void Perform(FarmWorld world, Player player, Point tile)
        {
            var c = TileMap.TileCenter(tile);
            if (world.IsWater(tile))
            {
                Refill();
                world.Effects.Emit("fx_ripple", c.X, c.Y, life: 0.6f);
                world.Float("Air penuh", Color.LightSkyBlue, player.X, player.Y - 90);
                return;
            }
            world.PlotAt(tile)!.Water();
            water--;
            world.Effects.Emit("fx_splash", c.X, c.Y + 6, life: 0.4f);
            world.Raise(GameEventType.Watered);
        }
    }

    /// <summary>
    /// Kelas dasar alat pukul (kapak, beliung). PEWARISAN bertingkat:
    /// Tool → StrikeTool → Axe / Pickaxe. Mencari target dan memberi damage ditulis sekali di sini.
    /// Mengayun ke udara kosong tetap boleh (animasi saja, tanpa energi), seperti di Stardew Valley.
    /// </summary>
    public abstract class StrikeTool : Tool
    {
        protected StrikeTool(string name, int power) : base(name, 3) => Power = power;

        public int Power { get; }

        protected override float EnergyCostAt(FarmWorld world, Point tile) => world.NodeAt(tile, this) == null ? 0 : EnergyCost;

        protected override string? Validate(FarmWorld world, Player player, Point tile) => null;

        public override bool IsUsefulAt(FarmWorld world, Player player, Point tile) => world.NodeAt(tile, this) != null;

        protected override void Perform(FarmWorld world, Player player, Point tile)
        {
            if (world.NodeAt(tile, this) is not { } node) return;
            world.Effects.Burst(node.HitParticle, node.HitPoint.X, node.HitPoint.Y - 20, 5, speed: 130);
            if (node.TakeDamage(Power)) world.DestroyNode(node);
        }
    }

    public sealed class Axe : StrikeTool
    {
        public Axe() : base("Kapak", 1) { }
        public override string IconName => "tool_axe";
        public override string? SwingName => "axe";
    }

    public sealed class Pickaxe : StrikeTool
    {
        public Pickaxe() : base("Beliung", 1) { }
        public override string IconName => "tool_pickaxe";
        public override string? SwingName => "pickaxe";
    }

    /// <summary>Kantong benih untuk satu jenis tanaman. Satu kelas, lima objek (satu per jenis).</summary>
    public sealed class SeedBag : Tool
    {
        public SeedBag(CropKind crop) : base(CropFactory.Info(crop).Name, 1) => Crop = crop;

        public CropKind Crop { get; }
        public SeedItem Seed => ItemDb.Seed(Crop);
        public override string IconName => Seed.IconName;
        public override string? Badge(Player player) => "x" + player.Inventory.Count(Seed);

        protected override string? Validate(FarmWorld world, Player player, Point tile)
        {
            var plot = world.PlotAt(tile);
            if (plot == null || !plot.IsEmpty) return "";
            if (player.Inventory.Count(Seed) <= 0) return "Tidak ada " + Seed.Name + ". Beli di toko Bu Lastri.";
            return null;
        }

        protected override void Perform(FarmWorld world, Player player, Point tile)
        {
            player.Inventory.TryRemove(Seed);
            var crop = CropFactory.Create(Crop);
            world.PlotAt(tile)!.Plant(crop);
            var c = TileMap.TileCenter(tile);
            world.Effects.Emit("fx_dust", c.X, c.Y + 10, life: 0.35f);
            if (!crop.GrowsIn(world.Clock.Season))
                world.Say(crop.Name + " tidak tumbuh di musim " + World.GameClock.SeasonName(world.Clock.Season) + "!");
            world.Raise(GameEventType.Planted);
        }
    }
}
