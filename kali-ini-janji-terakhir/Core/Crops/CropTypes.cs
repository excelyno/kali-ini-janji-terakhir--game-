using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.World;

namespace kali_ini_janji_terakhir.Core.Crops
{
    // PEWARISAN: semua jenis tanaman mewarisi Crop dan hanya mengisi bagian yang berbeda.
    // POLIMORFISME: GrowsIn, Yield, dan Regrows di-override sesuai sifat tanamannya.

    /// <summary>Lobak: cepat dan tahan dingin, satu-satunya yang tumbuh di musim Dingin.</summary>
    public sealed class Turnip : Crop
    {
        public override CropKind Kind => CropKind.Lobak;
        public override string Name => "Lobak";
        public override int GrowDays => 3;
        public override int SeedPrice => 15;
        public override int SellPrice => 35;
        public override Color FruitColor => Color.FromArgb(200, 120, 200);
        public override string Description => "Cepat panen, tumbuh di semua musim.";

        public override bool GrowsIn(Season season) => true;
    }

    public sealed class Carrot : Crop
    {
        public override CropKind Kind => CropKind.Wortel;
        public override string Name => "Wortel";
        public override int GrowDays => 4;
        public override int SeedPrice => 20;
        public override int SellPrice => 50;
        public override Color FruitColor => Color.OrangeRed;
        public override string Description => "Tanaman umbi, kaya vitamin A.";
    }

    /// <summary>Tomat: berbuah berulang. Setelah dipanen, tanaman kembali ke tahap remaja.</summary>
    public sealed class Tomato : Crop
    {
        public override CropKind Kind => CropKind.Tomat;
        public override string Name => "Tomat";
        public override int GrowDays => 5;
        public override int SeedPrice => 35;
        public override int SellPrice => 60;
        public override Color FruitColor => Color.Red;
        public override string Description => "Berbuah terus. Panen ulang tiap 2 hari.";

        public override bool Regrows => true;
        protected override int RegrowToAge => GrowDays - 2;
        public override bool GrowsIn(Season season) => season is Season.Semi or Season.Panas;
    }

    /// <summary>Jagung: lama, tapi satu batang menghasilkan dua tongkol.</summary>
    public sealed class Corn : Crop
    {
        public override CropKind Kind => CropKind.Jagung;
        public override string Name => "Jagung";
        public override int GrowDays => 6;
        public override int SeedPrice => 50;
        public override int SellPrice => 70;
        public override Color FruitColor => Color.Gold;
        public override string Description => "Hasil 2 tongkol per panen.";

        public override int Yield => 2;
        public override bool GrowsIn(Season season) => season is Season.Panas or Season.Gugur;
    }

    /// <summary>Labu: paling mahal dan paling lama, hanya tumbuh di musim Gugur dan Panas.</summary>
    public sealed class Pumpkin : Crop
    {
        public override CropKind Kind => CropKind.Labu;
        public override string Name => "Labu";
        public override int GrowDays => 8;
        public override int SeedPrice => 90;
        public override int SellPrice => 320;
        public override Color FruitColor => Color.DarkOrange;
        public override string Description => "Mahal dan lama. Musim Panas/Gugur.";

        public override bool GrowsIn(Season season) => season is Season.Gugur or Season.Panas;
    }

    /// <summary>Pabrik tanaman: mengubah CropKind menjadi objek konkret.</summary>
    public static class CropFactory
    {
        public static readonly CropKind[] All = (CropKind[])Enum.GetValues(typeof(CropKind));

        public static Crop Create(CropKind kind) => kind switch
        {
            CropKind.Lobak => new Turnip(),
            CropKind.Wortel => new Carrot(),
            CropKind.Tomat => new Tomato(),
            CropKind.Jagung => new Corn(),
            CropKind.Labu => new Pumpkin(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static readonly Crop[] samples = Array.ConvertAll(All, Create);

        /// <summary>Contoh tanaman untuk membaca data (harga, nama) tanpa menanam.</summary>
        public static Crop Info(CropKind kind) => samples[(int)kind];
    }
}
