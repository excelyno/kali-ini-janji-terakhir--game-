using System;
using System.Drawing;
using System.Linq;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        public static bool HasSave => SaveSystem.HasSave;

        public void Save()
        {
            if (won) return;

            var data = new SaveData(
                day,
                tm,
                gold,
                wood,
                stone,
                energy,
                water,
                waterCap,
                (int[])seeds.Clone(),
                (int[])crops.Clone(),
                soil.Select(kv => new SoilSave(kv.Key.X, kv.Key.Y, kv.Value.Watered, kv.Value.Crop, kv.Value.Age)).ToList(),
                removedIds.ToList(),
                px,
                py
            );

            SaveSystem.Save(data);
        }

        void ApplySave()
        {
            var d = SaveSystem.Load();
            if (d == null) return;

            day = d.Day;
            tm = d.Tm;
            gold = d.Gold;
            wood = d.Wood;
            stone = d.Stone;
            energy = d.Energy;
            water = d.Water;
            waterCap = d.WaterCap;
            px = d.Px;
            py = d.Py;

            Array.Copy(d.Seeds, seeds, Math.Min(seeds.Length, d.Seeds.Length));
            Array.Copy(d.Crops, crops, Math.Min(crops.Length, d.Crops.Length));

            soil.Clear();
            foreach (var s in d.Soil)
            {
                soil[new Point(s.X, s.Y)] = new Soil { Watered = s.Watered, Crop = s.Crop, Age = s.Age };
            }

            foreach (int id in d.Removed)
            {
                var o = objs.FirstOrDefault(q => q.Id == id);
                if (o == null) continue;
                objs.Remove(o);
                solids.Remove(o.Solid);
                removedIds.Add(id);
            }
        }
    }
}

