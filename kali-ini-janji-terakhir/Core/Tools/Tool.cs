using System.Drawing;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Tools
{
    /// <summary>
    /// ABSTRAKSI: alat yang dipegang pemain. Kelas ini memakai pola "template method":
    /// Use() selalu memeriksa syarat, memotong energi, lalu menjalankan efeknya,
    /// sedangkan isi pemeriksaan (Validate) dan efek (Perform) wajib diisi tiap alat.
    /// Pemain cukup memanggil ActiveTool.Use(...) tanpa peduli alat apa yang dipegang
    /// (POLIMORFISME).
    /// </summary>
    public abstract class Tool
    {
        protected Tool(string name, float energyCost)
        {
            Name = name;
            EnergyCost = energyCost;
        }

        public string Name { get; }
        public float EnergyCost { get; }

        /// <summary>Nama sprite ikon hotbar.</summary>
        public abstract string IconName { get; }

        /// <summary>Nama alat pada sprite ayunan "swing_{nama}_{arah}_{frame}"; null = tanpa ayunan (benih).</summary>
        public virtual string? SwingName => null;

        /// <summary>Teks kecil di slot hotbar (jumlah benih, isi air).</summary>
        public virtual string? Badge(Player player) => null;

        /// <summary>Warna kursor tile: true = putih (alat berguna di sini), false = merah.</summary>
        public virtual bool IsUsefulAt(FarmWorld world, Player player, Point tile) => Validate(world, player, tile) == null;

        /// <summary>Pakai alat ke tile sasaran pemain. Mengembalikan true jika berhasil.</summary>
        public bool Use(FarmWorld world, Player player)
        {
            var tile = player.TargetTile;
            string? error = Validate(world, player, tile);
            if (error != null)
            {
                if (error.Length > 0) world.Say(error);
                return false;
            }
            if (!player.TrySpendEnergy(EnergyCostAt(world, tile)))
            {
                world.Say("Terlalu lelah! Pulang dan tidur dulu.");
                return false;
            }
            Perform(world, player, tile);
            return true;
        }

        /// <summary>Biaya energi untuk aksi ini (mengisi air di kolam gratis).</summary>
        protected virtual float EnergyCostAt(FarmWorld world, Point tile) => EnergyCost;

        /// <summary>null = boleh dipakai; "" = gagal diam-diam; teks lain = pesan untuk pemain.</summary>
        protected abstract string? Validate(FarmWorld world, Player player, Point tile);

        protected abstract void Perform(FarmWorld world, Player player, Point tile);
    }
}
