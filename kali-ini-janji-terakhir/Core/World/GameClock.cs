using System;
using System.Drawing;

namespace kali_ini_janji_terakhir.Core.World
{
    public enum Season { Semi, Panas, Gugur, Dingin }

    /// <summary>
    /// Jam permainan. ENKAPSULASI: menit dan hari disimpan di field privat;
    /// dari luar hanya bisa dibaca atau dimajukan lewat method yang menjaga aturannya
    /// (tidak bisa mundur, hari selalu mulai jam 06:00, satu musim = 10 hari).
    /// </summary>
    public sealed class GameClock
    {
        private float minutes = GameConfig.DayStartMinute;
        private int day = 1;

        public int Day => day;
        public float Minutes => minutes;
        public int Hour => (int)(minutes / 60) % 24;
        public int Minute => (int)(minutes % 60);
        public int DayOfSeason => (day - 1) % GameConfig.DaysPerSeason + 1;
        public int SeasonIndex => (day - 1) / GameConfig.DaysPerSeason;
        public Season Season => (Season)(SeasonIndex % 4);
        public int Year => SeasonIndex / 4 + 1;
        public int DaysUntilNextSeason => GameConfig.DaysPerSeason - DayOfSeason + 1;
        public bool IsNight => minutes >= 1140 || minutes < 330;

        public string TimeText => $"{Hour:00}:{Minute / 10 * 10:00}";

        public static string SeasonName(Season s) => s switch
        {
            Season.Semi => "Semi",
            Season.Panas => "Panas",
            Season.Gugur => "Gugur",
            _ => "Dingin",
        };

        /// <summary>Majukan waktu. Mengembalikan true jika pemain kelewat larut dan pingsan.</summary>
        public bool Advance(float realSeconds)
        {
            if (realSeconds <= 0) return false;
            minutes += realSeconds * GameConfig.GameMinutesPerSecond;
            return minutes >= GameConfig.PassOutMinute;
        }

        /// <summary>Pindah ke pagi berikutnya. Mengembalikan true jika musim berganti.</summary>
        public bool StartNextDay()
        {
            int oldSeason = SeasonIndex;
            day++;
            minutes = GameConfig.DayStartMinute;
            return SeasonIndex != oldSeason;
        }

        public void Restore(int savedDay, float savedMinutes)
        {
            day = Math.Max(1, savedDay);
            minutes = Math.Clamp(savedMinutes, GameConfig.DayStartMinute, GameConfig.PassOutMinute - 1);
        }

        /// <summary>Warna lapisan langit (fajar, senja, malam) sebagai overlay semi transparan.</summary>
        public Color SkyOverlay()
        {
            float m = minutes;
            if (m < 420) return Color.FromArgb((int)Math.Clamp((420 - m) / 60f * 90, 0, 110), 30, 20, 80);
            if (m < 960) return Color.Transparent;
            if (m < 1140)
            {
                float t = (m - 960) / 180f;              // golden hour menuju senja
                return Color.FromArgb((int)(t * 90), 255, (int)(140 - 60 * t), 40);
            }
            float n = Math.Min(1f, (m - 1140) / 120f);   // malam
            return Color.FromArgb((int)(90 + n * 70), (int)(80 - 70 * n), (int)(40 - 30 * n), (int)(70 - 10 * n));
        }
    }
}
