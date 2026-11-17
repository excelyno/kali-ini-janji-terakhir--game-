using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Interfaces
{
    /// <summary>
    /// Kontrak untuk apa saja yang bisa diajak berinteraksi dengan tombol E:
    /// warga, ayam, bangunan, dan petak tanaman yang siap panen.
    /// Pemanggil cukup tahu interface ini, tidak perlu tahu kelas aslinya (polimorfisme).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Apakah pemain cukup dekat / menghadap objek ini.</summary>
        bool InRange(FarmWorld world);

        /// <summary>Teks petunjuk di HUD, misalnya "[E] Bicara dengan Kakek Wira".</summary>
        string Prompt(FarmWorld world);

        void Interact(FarmWorld world);
    }
}
