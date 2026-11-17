using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.World;

namespace kali_ini_janji_terakhir.Core.Items
{
    public enum ItemCategory { Benih, Hasil, Bahan }

    /// <summary>
    /// ABSTRAKSI: barang di tas. Setiap barang punya nama dan harga dasar,
    /// tetapi harga jual dan boleh-tidaknya dijual ditentukan oleh jenisnya masing-masing.
    /// </summary>
    public abstract class Item
    {
        protected Item(string id, string name, int basePrice, Color color)
        {
            Id = id;
            Name = name;
            BasePrice = basePrice;
            Color = color;
        }

        public string Id { get; }
        public string Name { get; }
        public int BasePrice { get; }
        public Color Color { get; }

        public abstract ItemCategory Category { get; }

        /// <summary>Nama sprite ikon barang (dibuat oleh AssetGen), misalnya "item_wortel".</summary>
        public virtual string IconName => "item_" + Id;

        /// <summary>Boleh dimasukkan ke peti jual? Benih tidak.</summary>
        public virtual bool Sellable => true;

        /// <summary>Harga jual sekarang. POLIMORFISME: hasil panen lebih mahal di luar musimnya.</summary>
        public virtual int SellPrice(Season season) => BasePrice;

        public override string ToString() => Name;
    }

    /// <summary>Benih untuk ditanam. Tidak dijual ke peti.</summary>
    public sealed class SeedItem : Item
    {
        public SeedItem(CropKind crop)
            : base("seed_" + crop.ToString().ToLowerInvariant(), "Benih " + CropFactory.Info(crop).Name,
                   CropFactory.Info(crop).SeedPrice, CropFactory.Info(crop).FruitColor)
        {
            Crop = crop;
        }

        public CropKind Crop { get; }
        public override ItemCategory Category => ItemCategory.Benih;
        public override string IconName => "seed_" + Crop.ToString().ToLowerInvariant();
        public override bool Sellable => false;
    }

    /// <summary>Hasil panen. Saat tanamannya tidak bisa tumbuh di musim ini, barangnya langka: +25%.</summary>
    public sealed class ProduceItem : Item
    {
        public ProduceItem(CropKind crop)
            : base(crop.ToString().ToLowerInvariant(), CropFactory.Info(crop).Name,
                   CropFactory.Info(crop).SellPrice, CropFactory.Info(crop).FruitColor)
        {
            Crop = crop;
        }

        public CropKind Crop { get; }
        public override ItemCategory Category => ItemCategory.Hasil;

        public override int SellPrice(Season season)
            => CropFactory.Info(Crop).GrowsIn(season) ? BasePrice : (int)Math.Round(BasePrice * 1.25);
    }

    /// <summary>Bahan dari alam atau ternak: kayu, batu, telur.</summary>
    public sealed class MaterialItem : Item
    {
        public MaterialItem(string id, string name, int price, Color color) : base(id, name, price, color) { }
        public override ItemCategory Category => ItemCategory.Bahan;
    }
}
