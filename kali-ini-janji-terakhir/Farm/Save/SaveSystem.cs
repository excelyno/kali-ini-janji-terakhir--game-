using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using kali_ini_janji_terakhir.Core.Economy;

namespace kali_ini_janji_terakhir.Farm
{
    public record PlotSave(int X, int Y, bool Watered, string? Crop, int Age);

    public record SaveData(
        int Version,
        int Day,
        float Minutes,
        int Coins,
        int TotalEarned,
        float Energy,
        int Water,
        int WaterCap,
        Dictionary<string, int> Items,
        List<PlotSave> Plots,
        List<int> Removed,
        List<int> EggsTaken,
        List<string> Flags,
        float Px,
        float Py,
        DebtSave Debt,
        int QuestIndex,
        int QuestProgress,
        List<int>? Stumps
    );

    /// <summary>Menyimpan dan memuat progres ke %APPDATA%/KaliIniJanjiTerakhir. Ditulis atomik (file .tmp lalu ganti).</summary>
    public static class SaveSystem
    {
        public const int Version = 3;   // v3: peta tile baru (farm.txt), tunggul pohon

        public static string SavePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KaliIniJanjiTerakhir",
            "save_v3.json");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            try
            {
                string? dir = Path.GetDirectoryName(SavePath);
                if (dir != null) Directory.CreateDirectory(dir);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data));
                File.Move(tmp, SavePath, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Save failed: " + ex.Message);
            }
        }

        public static SaveData? Load()
        {
            try
            {
                if (!HasSave) return null;
                var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath));
                return d != null && d.Version == Version ? d : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Load failed: " + ex.Message);
                return null;
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (HasSave) File.Delete(SavePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Delete save failed: " + ex.Message);
            }
        }
    }
}
