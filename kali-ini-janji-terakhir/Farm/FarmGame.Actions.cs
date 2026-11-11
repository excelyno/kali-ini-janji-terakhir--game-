using System;
using System.Drawing;
using System.Linq;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        void UseTool()
        {
            if (cooldown > 0) return;
            cooldown = 0.28f;
            var tp = TargetPoint();
            var tt = TargetTile();
            var tile = new Rectangle(tt.X * T, tt.Y * T, T, T);
            var key = tt;

            if (slot == 0)
            {
                if (!farmZone.Contains(tile.Location)) { Say("Cangkul hanya di lahan pertanian."); return; }
                if (soil.ContainsKey(key)) return;
                if (solids.Any(s => s.IntersectsWith(tile)) || objs.Any(o => o.Bounds.IntersectsWith(tile) && o.Solid.IntersectsWith(tile))) { Say("Ada penghalang."); return; }
                if (!Spend(2)) return;
                soil[key] = new Soil();
                Float("Digali", Color.White, tile.X + 10, tile.Y);
            }
            else if (slot == 1)
            {
                if (water < 1) { Say("Air habis! Isi di kolam (E)."); return; }
                if (soil.TryGetValue(key, out var s) && !s.Watered)
                {
                    if (!Spend(1)) return;
                    water--;
                    s.Watered = true;
                    Float("Disiram", Color.LightSkyBlue, tile.X + 6, tile.Y);
                }
            }
            else if (slot == 2 || slot == 3)
            {
                bool wantTree = slot == 2;
                var o = objs.FirstOrDefault(q => (wantTree ? q.Kind == ObjKind.Tree : q.Kind == ObjKind.Rock) && Rectangle.Inflate(q.Bounds, 12, 12).Contains(tp) && !(q.Y > WH - 20 || q.Y < 160 || q.X < 40 || q.X > WW - 40));
                if (o == null) { Say(wantTree ? "Hadapkan ke pohon." : "Hadapkan ke batu."); return; }
                if (!Spend(3)) return;
                o.Hp--;
                Float("Tok!", Color.White, o.X - 12, o.Y - 40);
                if (o.Hp <= 0)
                {
                    objs.Remove(o);
                    solids.Remove(o.Solid);
                    removedIds.Add(o.Id);
                    int n = wantTree ? 3 : 2;
                    if (wantTree) wood += n; else stone += n;
                    Float("+" + n + (wantTree ? " kayu" : " batu"), Color.Gold, o.X - 20, o.Y - 70);
                }
            }
            else
            {
                int c = slot - 4;
                if (soil.TryGetValue(key, out var s) && s.Crop < 0)
                {
                    if (seeds[c] <= 0) { Say("Tidak ada bibit " + CropName[c] + "."); return; }
                    seeds[c]--;
                    s.Crop = c;
                    s.Age = 0;
                    Float("Tanam " + CropName[c], Color.LightGreen, tile.X - 10, tile.Y);
                }
            }
        }

        bool Spend(float e)
        {
            if (energy < e) { Say("Terlalu lelah! Tidur (Z)."); return false; }
            energy -= e;
            return true;
        }

        void Interact()
        {
            var foot = new Rectangle((int)px - 10, (int)py - 10, 20, 20);
            if (foot.IntersectsWith(shopZone)) { shopOpen = true; return; }
            if (foot.IntersectsWith(binZone)) { SellAll(); return; }
            var tt = TargetTile();
            if (soil.TryGetValue(tt, out var s) && s.Crop >= 0)
            {
                if (s.Age >= Grow[s.Crop])
                {
                    crops[s.Crop]++;
                    Float("+1 " + CropName[s.Crop], CropCol[s.Crop], tt.X * T, tt.Y * T - 10);
                    s.Crop = -1;
                    s.Age = 0;
                }
                else
                {
                    Say(CropName[s.Crop] + ": " + s.Age + "/" + Grow[s.Crop] + " hari.");
                }
                return;
            }
            if (foot.IntersectsWith(pondZone)) { water = waterCap; Float("Air penuh", Color.LightSkyBlue, px - 20, py - 60); return; }
            if (foot.IntersectsWith(Rectangle.Inflate(houseRect, 30, 20))) { Say("Rumah: tempat beristirahat. Tekan Z untuk tidur."); return; }
        }

        void SellAll()
        {
            int total = wood * 10 + stone * 12;
            for (int i = 0; i < 4; i++) total += crops[i] * Sell[i];
            if (total == 0) { Say("Tidak ada yang dijual."); return; }
            gold += total;
            wood = stone = 0;
            Array.Clear(crops);
            Float("+" + total + " emas", Color.Gold, px - 20, py - 70);
            if (gold >= GoalGold)
            {
                won = true;
                SaveSystem.DeleteSave();
            }
        }

        void Buy(int i)
        {
            int[] price = { 20, 35, 50, 90, 30, 300, 0, 0 };
            if (i < 4)
            {
                if (gold < SeedPrice[i]) { Say("Emas tidak cukup."); return; }
                gold -= SeedPrice[i];
                seeds[i]++;
            }
            else if (i == 4)
            {
                if (gold < price[4]) { Say("Emas tidak cukup."); return; }
                gold -= 30;
                energy = 100;
            }
            else if (i == 5)
            {
                if (waterCap > 12) { Say("Sudah dimiliki."); return; }
                if (gold < 300) { Say("Emas tidak cukup."); return; }
                gold -= 300;
                waterCap = 30;
                water = 30;
            }
            else
            {
                shopOpen = false;
            }
        }

        void SleepNow()
        {
            foreach (var s in soil.Values)
            {
                if (s.Crop >= 0 && s.Watered && s.Age < Grow[s.Crop]) s.Age++;
                s.Watered = false;
            }
            day++;
            tm = 360;
            energy = 100;
            water = waterCap;
            Say("Hari ke-" + day + ". Selamat pagi!");
            Save();
        }
    }
}

