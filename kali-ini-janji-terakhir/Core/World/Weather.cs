namespace kali_ini_janji_terakhir.Core.World
{
    public enum WeatherKind { Cerah, Berawan, Hujan, Salju }

    /// <summary>
    /// Cuaca ditentukan dari nomor hari (deterministik), jadi tidak perlu disimpan.
    /// Hujan menyiram semua petak secara otomatis.
    /// </summary>
    public static class Weather
    {
        public static WeatherKind ForDay(int day, Season season)
        {
            if (day == 1) return WeatherKind.Cerah;
            uint h = (uint)day * 2654435761u;
            h ^= h >> 15;
            int roll = (int)(h % 100);
            return season switch
            {
                Season.Semi => roll < 30 ? WeatherKind.Hujan : roll < 55 ? WeatherKind.Berawan : WeatherKind.Cerah,
                Season.Panas => roll < 10 ? WeatherKind.Hujan : roll < 25 ? WeatherKind.Berawan : WeatherKind.Cerah,
                Season.Gugur => roll < 25 ? WeatherKind.Hujan : roll < 60 ? WeatherKind.Berawan : WeatherKind.Cerah,
                _ => roll < 40 ? WeatherKind.Salju : roll < 70 ? WeatherKind.Berawan : WeatherKind.Cerah,
            };
        }

        public static string Name(WeatherKind w) => w.ToString();
        public static bool WatersCrops(WeatherKind w) => w == WeatherKind.Hujan;
    }
}
