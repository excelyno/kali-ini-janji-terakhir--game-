using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Economy
{
    /// <summary>
    /// ABSTRAKSI: satu baris barang di toko. TryBuy() (template method) selalu memeriksa
    /// ketersediaan dan saldo, lalu memanggil Deliver() yang isinya berbeda di tiap subclass.
    /// Jendela toko cukup memanggil offer.TryBuy() untuk semua jenis barang (POLIMORFISME).
    /// </summary>
    public abstract class ShopOffer
    {
        protected ShopOffer(string name, int price)
        {
            Name = name;
            Price = price;
        }

        public string Name { get; }
        public int Price { get; }

        /// <summary>Ikon di daftar toko.</summary>
        public abstract string IconName { get; }

        public abstract string Description(FarmWorld world);

        /// <summary>null = bisa dibeli; teks = alasan tidak bisa (misal "Dimiliki").</summary>
        public virtual string? Unavailable(FarmWorld world) => null;

        protected abstract void Deliver(FarmWorld world, Player player);

        public bool TryBuy(FarmWorld world)
        {
            string? reason = Unavailable(world);
            if (reason != null) { world.Say(reason); return false; }
            if (!world.Player.Wallet.TrySpend(Price)) { world.Say("Koin tidak cukup."); return false; }
            Deliver(world, world.Player);
            return true;
        }
    }

    public sealed class SeedOffer : ShopOffer
    {
        private readonly SeedItem seed;

        public SeedOffer(CropKind kind) : base(ItemDb.Seed(kind).Name, ItemDb.Seed(kind).BasePrice) => seed = ItemDb.Seed(kind);

        public override string IconName => seed.IconName;

        public override string Description(FarmWorld world)
        {
            var crop = CropFactory.Info(seed.Crop);
            string season = crop.GrowsIn(world.Clock.Season) ? "cocok musim ini" : "TIDAK tumbuh musim ini";
            return $"{crop.GrowDays} hari, jual {crop.SellPrice}. {crop.Description} ({season}) - punya {world.Player.Inventory.Count(seed)}";
        }

        protected override void Deliver(FarmWorld world, Player player)
        {
            player.Inventory.Add(seed);
            world.Say("Membeli " + seed.Name + ".");
        }
    }

    public sealed class MealOffer : ShopOffer
    {
        private readonly float energy;

        public MealOffer(string name, int price, float energy) : base(name, price) => this.energy = energy;

        public override string IconName => "ui_icon_energy";

        public override string Description(FarmWorld world) => $"Pulihkan {energy:0} energi.";

        public override string? Unavailable(FarmWorld world)
            => world.Player.Energy >= GameConfig.MaxEnergy ? "Energi penuh" : null;

        protected override void Deliver(FarmWorld world, Player player)
        {
            player.RestoreEnergy(energy);
            world.Say("Nyam! Energi pulih.");
        }
    }

    public sealed class CanUpgradeOffer : ShopOffer
    {
        public CanUpgradeOffer() : base("Penyiram Besar", 300) { }

        public override string IconName => "tool_can";

        public override string Description(FarmWorld world) => $"Kapasitas air {Tools.WateringCan.BigCapacity}.";

        public override string? Unavailable(FarmWorld world) => world.Player.Can.IsUpgraded ? "Dimiliki" : null;

        protected override void Deliver(FarmWorld world, Player player)
        {
            player.Can.Upgrade();
            world.Say("Penyiram sekarang muat " + player.Can.Capacity + " air!");
        }
    }
}
