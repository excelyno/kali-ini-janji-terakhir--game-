using kali_ini_janji_terakhir.Core.Tools;

namespace kali_ini_janji_terakhir.Core.Interfaces
{
    /// <summary>Objek yang bisa dipukul alat dan hancur (pohon, batu).</summary>
    public interface IDamageable
    {
        int Hp { get; }
        int MaxHp { get; }
        bool IsDestroyed { get; }

        /// <summary>Apakah alat ini cocok (kapak untuk pohon, beliung untuk batu).</summary>
        bool CanBeHitBy(Tool tool);

        /// <summary>Kurangi HP. Mengembalikan true jika objek hancur oleh pukulan ini.</summary>
        bool TakeDamage(int amount);
    }
}
