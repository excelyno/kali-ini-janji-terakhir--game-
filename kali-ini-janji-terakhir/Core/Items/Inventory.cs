using System;
using System.Collections.Generic;
using System.Linq;

namespace kali_ini_janji_terakhir.Core.Items
{
    /// <summary>
    /// Tas pemain. ENKAPSULASI: dictionary isinya privat. Luar kelas hanya bisa
    /// menambah (Add), mengambil (TryRemove), dan membaca (Count / Entries),
    /// sehingga jumlah barang tidak pernah negatif.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<Item, int> items = new();

        public event Action? Changed;

        public int Count(Item item) => items.TryGetValue(item, out int n) ? n : 0;

        public void Add(Item item, int amount = 1)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            items[item] = Count(item) + amount;
            Changed?.Invoke();
        }

        public bool TryRemove(Item item, int amount = 1)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int have = Count(item);
            if (have < amount) return false;
            if (have == amount) items.Remove(item); else items[item] = have - amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Isi tas (salinan baca-saja), urut sesuai kategori lalu nama.</summary>
        public IReadOnlyList<KeyValuePair<Item, int>> Entries =>
            items.OrderBy(kv => kv.Key.Category).ThenBy(kv => kv.Key.Name).ToList();

        /// <summary>Ambil semua barang yang bisa dijual. Mengembalikan daftar yang diambil.</summary>
        public List<KeyValuePair<Item, int>> TakeAllSellable()
        {
            var taken = items.Where(kv => kv.Key.Sellable).ToList();
            foreach (var kv in taken) items.Remove(kv.Key);
            if (taken.Count > 0) Changed?.Invoke();
            return taken;
        }

        public Dictionary<string, int> ToSave() => items.ToDictionary(kv => kv.Key.Id, kv => kv.Value);

        public void Restore(Dictionary<string, int>? saved)
        {
            items.Clear();
            if (saved == null) return;
            foreach (var (id, n) in saved)
            {
                var item = ItemDb.ById(id);
                if (item != null && n > 0) items[item] = n;
            }
            Changed?.Invoke();
        }
    }
}
