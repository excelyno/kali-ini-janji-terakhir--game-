using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using kali_ini_janji_terakhir.Core.Crops;

namespace kali_ini_janji_terakhir.Core.Items
{
    /// <summary>Daftar semua barang. Setiap barang adalah satu objek tunggal (dipakai sebagai kunci tas).</summary>
    public static class ItemDb
    {
        public static readonly MaterialItem Wood = new("wood", "Kayu", 10, Color.SaddleBrown);
        public static readonly MaterialItem Stone = new("stone", "Batu", 12, Color.Gray);
        public static readonly MaterialItem Egg = new("egg", "Telur", 25, Color.Beige);

        private static readonly SeedItem[] seeds = CropFactory.All.Select(k => new SeedItem(k)).ToArray();
        private static readonly ProduceItem[] produce = CropFactory.All.Select(k => new ProduceItem(k)).ToArray();

        public static SeedItem Seed(CropKind kind) => seeds[(int)kind];
        public static ProduceItem Produce(CropKind kind) => produce[(int)kind];

        public static IEnumerable<Item> All =>
            new Item[] { Wood, Stone, Egg }.Concat(seeds).Concat(produce);

        private static readonly Dictionary<string, Item> byId = All.ToDictionary(i => i.Id, StringComparer.Ordinal);

        public static Item? ById(string id) => byId.TryGetValue(id, out var item) ? item : null;
    }
}
