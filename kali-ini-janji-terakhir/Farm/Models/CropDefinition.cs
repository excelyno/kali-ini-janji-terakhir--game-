using System.Drawing;
using System.Linq;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Metadata defining a crop's growth duration, purchase price, selling value, and display color.
    /// </summary>
    public sealed record CropDefinition(int Id, string Name, int GrowDays, int SeedPrice, int SellPrice, Color Color);

    /// <summary>
    /// Central registry of available crops and tool slots in the game.
    /// Scalable: simply append new CropDefinition items to add new harvestable crops.
    /// </summary>
    public static class CropCatalog
    {
        public static readonly CropDefinition[] Crops =
        {
            new(0, "Wortel", 3, 20, 45, Color.OrangeRed),
            new(1, "Tomat", 4, 35, 90, Color.Red),
            new(2, "Jagung", 5, 50, 150, Color.Gold),
            new(3, "Labu", 7, 90, 340, Color.DarkOrange),
        };

        public static readonly string[] SlotNames =
        {
            "Cangkul", "Penyiram", "Kapak", "Beliung",
            "Wortel", "Tomat", "Jagung", "Labu"
        };

        public static string[] CropNames { get; } = Crops.Select(c => c.Name).ToArray();
        public static int[] GrowDays { get; } = Crops.Select(c => c.GrowDays).ToArray();
        public static int[] SellPrices { get; } = Crops.Select(c => c.SellPrice).ToArray();
        public static int[] SeedPrices { get; } = Crops.Select(c => c.SeedPrice).ToArray();
        public static Color[] CropColors { get; } = Crops.Select(c => c.Color).ToArray();
    }
}

