using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Interfaces
{
    /// <summary>Objek yang berubah setiap pagi (ayam bertelur, pohon tumbuh kembali, dll).</summary>
    public interface IDailyUpdatable
    {
        void OnNewDay(FarmWorld world);
    }
}
