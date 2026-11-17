namespace kali_ini_janji_terakhir.Core
{
    /// <summary>Konstanta global permainan (ukuran peta, durasi hari, energi).</summary>
    public static class GameConfig
    {
        public const int Tile = 48;
        public const int MapTilesW = 40;
        public const int MapTilesH = 30;
        public const int WorldW = MapTilesW * Tile;
        public const int WorldH = MapTilesH * Tile;

        public const float DayStartMinute = 360f;   // 06:00
        public const float PassOutMinute = 1440f;   // 24:00
        public const float GameMinutesPerSecond = 3f;
        public const int DaysPerSeason = 10;

        public const float MaxEnergy = 100f;
        public const float WalkSpeed = 190f;
        public const float RunSpeed = 290f;
    }
}
