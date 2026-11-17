using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Crops
{
    /// <summary>
    /// Satu petak tanah yang sudah dicangkul.
    /// ENKAPSULASI: status siram dan tanaman hanya bisa diubah lewat Water/Plant/Harvest,
    /// sehingga tidak mungkin, misalnya, menanam dua tanaman di satu petak.
    /// Petak juga IInteractable: tombol E di depan petak matang = panen.
    /// </summary>
    public sealed class FarmPlot : IInteractable, IWorldDrawable
    {
        public FarmPlot(Point tile) => Tile = tile;

        public Point Tile { get; }
        public bool IsWatered { get; private set; }
        public Crop? Crop { get; private set; }
        public bool IsEmpty => Crop == null;
        public Rectangle Area => new(Tile.X * GameConfig.Tile, Tile.Y * GameConfig.Tile, GameConfig.Tile, GameConfig.Tile);

        public bool Water()
        {
            if (IsWatered) return false;
            IsWatered = true;
            return true;
        }

        public bool Plant(Crop crop)
        {
            if (!IsEmpty) return false;
            Crop = crop;
            return true;
        }

        public HarvestResult? Harvest()
        {
            if (Crop == null || !Crop.IsRipe) return null;
            var result = Crop.Harvest();
            if (!result.PlantStays) Crop = null;
            return result;
        }

        /// <summary>Pergantian hari: tanaman yang disiram tumbuh, lalu tanah mengering (kecuali hujan).</summary>
        public void OnNewDay(Season season, bool rainToday)
        {
            if (Crop != null && IsWatered) Crop.Grow(season);
            IsWatered = rainToday;
        }

        public void Restore(bool watered, Crop? crop)
        {
            IsWatered = watered;
            Crop = crop;
        }

        // --- IWorldDrawable: tanaman ikut diurutkan dengan benda lain (jagung tinggi bisa menutupi pemain) ---
        public float SortY => Area.Bottom - 8;
        public RectangleF Bounds => new(Area.X, Area.Y - 32, Area.Width, Area.Height + 32);

        public void Draw(Renderer r, float time)
        {
            if (Crop == null) return;
            float sway = Crop.IsRipe ? MathF.Sin(time * 2f + Tile.X) * 1.5f : 0;
            r.Draw(Crop.SpriteName, Area.X + Area.Width / 2f + sway, Area.Bottom - 6);
        }

        // --- IInteractable ---
        public bool InRange(FarmWorld world) => Crop is { IsRipe: true } && world.Player.TargetTile == Tile;
        public string Prompt(FarmWorld world) => "Panen " + Crop!.Name;
        public void Interact(FarmWorld world) => world.HarvestPlot(this);
    }
}
