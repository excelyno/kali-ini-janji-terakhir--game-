using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.World;

namespace kali_ini_janji_terakhir.Core.Crops
{
    public enum CropKind { Lobak, Wortel, Tomat, Jagung, Labu }

    /// <summary>Hasil sekali panen: berapa buah, dan apakah tanamannya tetap hidup.</summary>
    public readonly record struct HarvestResult(CropKind Kind, int Amount, bool PlantStays);

    /// <summary>
    /// ABSTRAKSI: kelas dasar semua tanaman. "Tanaman" sebagai konsep tidak bisa dibuat
    /// langsung (abstract); yang ditanam selalu jenis konkret seperti Wortel atau Tomat.
    /// Aturan umum (umur, tahap tumbuh, panen, nama sprite) ada di sini, sedangkan detail per jenis
    /// (lama tumbuh, musim, harga, hasil) diisi oleh kelas turunan.
    /// </summary>
    public abstract class Crop
    {
        // ENKAPSULASI: umur hanya berubah lewat Grow()/Harvest()/RestoreAge().
        private int age;

        public abstract CropKind Kind { get; }
        public abstract string Name { get; }
        public abstract int GrowDays { get; }
        public abstract int SeedPrice { get; }
        public abstract int SellPrice { get; }
        public abstract Color FruitColor { get; }
        public abstract string Description { get; }

        /// <summary>Banyak hasil per panen. Jagung override jadi 2.</summary>
        public virtual int Yield => 1;

        /// <summary>Tanaman yang berbuah berulang (tomat) override ini.</summary>
        public virtual bool Regrows => false;
        protected virtual int RegrowToAge => 0;

        public int Age => age;
        public bool IsRipe => age >= GrowDays;
        public float Progress => Math.Clamp(age / (float)GrowDays, 0f, 1f);

        /// <summary>0 = benih, 1 = tunas, 2 = remaja, 3 = siap panen.</summary>
        public int Stage => IsRipe ? 3 : age == 0 ? 0 : Progress < 0.5f ? 1 : 2;

        /// <summary>Musim tumbuh. Default: semua musim kecuali Dingin.</summary>
        public virtual bool GrowsIn(Season season) => season != Season.Dingin;

        /// <summary>Dipanggil tiap pagi jika petaknya disiram.</summary>
        public virtual void Grow(Season season)
        {
            if (!IsRipe && GrowsIn(season)) age++;
        }

        public HarvestResult Harvest()
        {
            if (!IsRipe) throw new InvalidOperationException(Name + " belum siap dipanen.");
            if (Regrows) age = RegrowToAge;
            return new HarvestResult(Kind, Yield, Regrows);
        }

        public void RestoreAge(int savedAge) => age = Math.Clamp(savedAge, 0, GrowDays);

        /// <summary>Nama sprite untuk tahap sekarang, misalnya "crop_tomat_2" (dibuat oleh AssetGen).</summary>
        public string SpriteName => $"crop_{Kind.ToString().ToLowerInvariant()}_{Stage}";
    }
}
