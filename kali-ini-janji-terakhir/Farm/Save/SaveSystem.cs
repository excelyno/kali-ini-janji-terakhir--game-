using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace kali_ini_janji_terakhir.Farm
{
    public record SoilSave(int X, int Y, bool Watered, int Crop, int Age);

    public record SaveData(
        int Day,
        float Tm,
        int Gold,
        int Wood,
        int Stone,
        float Energy,
        float Water,
        int WaterCap,
        int[] Seeds,
        int[] Crops,
        List<SoilSave> Soil,
        List<int> Removed,
        float Px,
        float Py
    );

    /// <summary>
    /// Handles persisting and restoring the player's farm progress to/from local storage.
    /// </summary>
    public static class SaveSystem
    {
        public static string SavePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KaliIniJanjiTerakhir",
            "save.json"
        );

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            try
            {
                string? dir = Path.GetDirectoryName(SavePath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(SavePath, JsonSerializer.Serialize(data));
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
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath));
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

