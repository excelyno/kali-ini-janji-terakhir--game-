using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed class FarmGame : IDisposable
    {
        const int T = 48, MW = 40, MH = 30, WW = MW * T, WH = MH * T;
        const int GoalGold = 5000;
        static readonly string[] CropName = { "Wortel", "Tomat", "Jagung", "Labu" };
        static readonly int[] Grow = { 3, 4, 5, 7 };
        static readonly int[] Sell = { 45, 90, 150, 340 };
        static readonly int[] SeedPrice = { 20, 35, 50, 90 };
        static readonly Color[] CropCol = { Color.OrangeRed, Color.Red, Color.Gold, Color.DarkOrange };
        static readonly string[] SlotName = { "Cangkul", "Penyiram", "Kapak", "Beliung", "Wortel", "Tomat", "Jagung", "Labu" };

        class Soil { public bool Watered; public int Crop = -1; public int Age; }
        class Obj { public bool Tree; public int Hp = 3; public float X, Y; public Rectangle Solid; public Rectangle Bounds; }
        class Floater { public string Text = ""; public float X, Y, Life = 1.4f; public Color C; }

        readonly Dictionary<Point, Soil> soil = new();
        readonly List<Obj> objs = new();
        readonly List<Rectangle> solids = new();
        readonly List<Floater> floaters = new();
        readonly HashSet<Keys> held = new();
        readonly Font fSmall = new("Segoe UI", 9f, FontStyle.Bold), fMed = new("Segoe UI", 12f, FontStyle.Bold), fBig = new("Segoe UI", 26f, FontStyle.Bold);
        readonly Bitmap ground;
        readonly Rectangle farmZone = new(6 * T, 12 * T, 24 * T, 8 * T);
        readonly Rectangle pond = new(5 * T, 25 * T, 6 * T, 4 * T);
        readonly Rectangle shopRect = new(1440, 270, 288, 210);
        Rectangle binRect, shopZone, binZone, pondZone;

        float px = WW / 2f, py = 1040;
        int fdx = 0, fdy = 1;
        bool moving; float walk;
        int vw = 1280, vh = 720;
        float camX, camY;
        float tm = 360f; int day = 1;
        int gold = 100, wood, stone, slot;
        float energy = 100, water = 12; int waterCap = 12;
        readonly int[] seeds = { 5, 0, 0, 0 };
        readonly int[] crops = new int[4];
        float anim; float cooldown;
        bool shopOpen; int shopSel;
        string msg = ""; float msgT;
        bool won;

        public FarmGame()
        {
            Sprites.Load();
            binRect = new Rectangle(1380, 400, Sprites.Crates.Width, 84);
            shopZone = new Rectangle(shopRect.X + 60, shopRect.Bottom - 20, shopRect.Width - 120, 70);
            binZone = Rectangle.Inflate(binRect, 36, 36);
            pondZone = Rectangle.Inflate(pond, 50, 50);

            solids.Add(new Rectangle(shopRect.X, shopRect.Y + 110, shopRect.Width, 100));
            solids.Add(new Rectangle(binRect.X, binRect.Bottom - 24, binRect.Width, 24));
            solids.Add(Rectangle.Inflate(pond, -10, -10));
            int fw = Sprites.Fence.Width;
            for (int x = farmZone.X; x < farmZone.Right; x += fw)
                if (x < 16 * T || x > 20 * T) solids.Add(new Rectangle(x, 11 * T + 28, fw, 12));

            var rnd = new Random(7);
            for (int i = 0; i < 70; i++)
            {
                bool tree = i % 2 == 0;
                float x = rnd.Next(60, WW - 60), y = rnd.Next(200, WH - 40);
                var foot = new Rectangle((int)x - 40, (int)y - 40, 80, 50);
                if (Math.Abs(x - px) < 150 && Math.Abs(y - py) < 150) continue;
                if (foot.IntersectsWith(Rectangle.Inflate(shopRect, 40, 80)) || foot.IntersectsWith(Rectangle.Inflate(binRect, 60, 60)) || foot.IntersectsWith(Rectangle.Inflate(pond, 40, 40))) continue;
                if (objs.Any(o => Math.Abs(o.X - x) < 90 && Math.Abs(o.Y - y) < 60)) continue;
                if (foot.IntersectsWith(new Rectangle(16 * T, 10 * T, 4 * T, 3 * T))) continue;
                AddObj(tree, x, y);
            }
            for (int x = 30; x < WW; x += 130) { AddObj(true, x, 150); AddObj(true, x + 60, WH - 10); }
            for (int y = 300; y < WH - 100; y += 160) { AddObj(true, 24, y); AddObj(true, WW - 24, y); }

            ground = BuildGround();
        }

        void AddObj(bool tree, float x, float y)
        {
            var o = new Obj { Tree = tree, X = x, Y = y };
            var s = tree ? Sprites.Tree : Sprites.Rock;
            o.Bounds = new Rectangle((int)x - s.Width / 2, (int)y - s.Height, s.Width, s.Height);
            o.Solid = tree ? new Rectangle((int)x - 14, (int)y - 14, 28, 14) : new Rectangle((int)x - s.Width / 2 + 6, (int)y - s.Height / 2, s.Width - 12, s.Height / 2);
            objs.Add(o);
            solids.Add(o.Solid);
        }

        Bitmap BuildGround()
        {
            var bmp = new Bitmap(WW, WH, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(96, 168, 72));
            var r = new Random(3);
            Color[] gc = { Color.FromArgb(88, 158, 66), Color.FromArgb(108, 182, 80), Color.FromArgb(78, 146, 60) };
            for (int i = 0; i < 9000; i++)
            {
                using var b = new SolidBrush(gc[r.Next(3)]);
                g.FillRectangle(b, r.Next(WW), r.Next(WH), 6, 6);
            }
            using var path = new SolidBrush(Color.FromArgb(196, 160, 104));
            g.FillRectangle(path, 16 * T, 10 * T, 4 * T, 3 * T);
            g.FillRectangle(path, 18 * T, 480, 2 * T, 12 * T);
            g.FillRectangle(path, 1440, 480, 288, 60);
            g.FillRectangle(path, 18 * T, 10 * T, 22 * T, T);
            using var dirt = new Pen(Color.FromArgb(60, 120, 90, 50), 2);
            g.DrawRectangle(dirt, farmZone);
            return bmp;
        }

        public void Dispose()
        {
            ground.Dispose(); fSmall.Dispose(); fMed.Dispose(); fBig.Dispose();
        }

        // ---------- input ----------
        public void Resize(int w, int h) { vw = Math.Max(100, w); vh = Math.Max(100, h); }
        public void LoseFocus() => held.Clear();
        public void KeyUp(Keys k) => held.Remove(k);
        public void Wheel(int delta) { if (!shopOpen) slot = (slot + (delta > 0 ? 7 : 1)) % 8; }
        public void Click() { if (!shopOpen) UseTool(); }

        public void KeyDown(Keys k)
        {
            bool fresh = held.Add(k);
            if (shopOpen)
            {
                if (!fresh) return;
                if (k == Keys.Up || k == Keys.W) shopSel = (shopSel + 7) % 8;
                else if (k == Keys.Down || k == Keys.S) shopSel = (shopSel + 1) % 8;
                else if (k == Keys.Enter || k == Keys.Space) Buy(shopSel);
                else if (k == Keys.Escape || k == Keys.E) shopOpen = false;
                return;
            }
            if (!fresh) return;
            if (k >= Keys.D1 && k <= Keys.D8) slot = k - Keys.D1;
            else if (k == Keys.Space) UseTool();
            else if (k == Keys.E) Interact();
            else if (k == Keys.Z) SleepNow();
        }

        // ---------- logic ----------
        void Say(string s) { msg = s; msgT = 2.5f; }
        void Float(string s, Color c, float x, float y) => floaters.Add(new Floater { Text = s, X = x, Y = y, C = c });

        Point TargetPoint() => new((int)(px + fdx * 36), (int)(py - 10 + fdy * 36));
        Point TargetTile() { var p = TargetPoint(); return new Point(Math.Clamp(p.X / T, 0, MW - 1), Math.Clamp(p.Y / T, 0, MH - 1)); }

        bool Blocked(float x, float y)
        {
            var r = new Rectangle((int)x - 12, (int)y - 12, 24, 12);
            if (x < 20 || x > WW - 20 || y < 40 || y > WH - 10) return true;
            foreach (var s in solids) if (s.IntersectsWith(r)) return true;
            return false;
        }

        void UseTool()
        {
            if (cooldown > 0) return;
            cooldown = 0.28f;
            var tp = TargetPoint(); var tt = TargetTile();
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
                    water--; s.Watered = true;
                    Float("Disiram", Color.LightSkyBlue, tile.X + 6, tile.Y);
                }
            }
            else if (slot == 2 || slot == 3)
            {
                bool wantTree = slot == 2;
                var o = objs.FirstOrDefault(q => q.Tree == wantTree && Rectangle.Inflate(q.Bounds, 12, 12).Contains(tp) && !(q.Y > WH - 20 || q.Y < 160 || q.X < 40 || q.X > WW - 40));
                if (o == null) { Say(wantTree ? "Hadapkan ke pohon." : "Hadapkan ke batu."); return; }
                if (!Spend(3)) return;
                o.Hp--;
                Float("Tok!", Color.White, o.X - 12, o.Y - 40);
                if (o.Hp <= 0)
                {
                    objs.Remove(o); solids.Remove(o.Solid);
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
                    seeds[c]--; s.Crop = c; s.Age = 0;
                    Float("Tanam " + CropName[c], Color.LightGreen, tile.X - 10, tile.Y);
                }
            }
        }

        bool Spend(float e)
        {
            if (energy < e) { Say("Terlalu lelah! Tidur (Z)."); return false; }
            energy -= e; return true;
        }

        void Interact()
        {
            var foot = new Rectangle((int)px - 10, (int)py - 10, 20, 20);
            if (foot.IntersectsWith(shopZone)) { shopOpen = true; return; }
            if (foot.IntersectsWith(binZone)) { SellAll(); return; }
            var tt = TargetTile();
            if (soil.TryGetValue(tt, out var s) && s.Crop >= 0)
            {
                if (s.Age >= Grow[s.Crop]) { crops[s.Crop]++; Float("+1 " + CropName[s.Crop], CropCol[s.Crop], tt.X * T, tt.Y * T - 10); s.Crop = -1; s.Age = 0; }
                else Say(CropName[s.Crop] + ": " + s.Age + "/" + Grow[s.Crop] + " hari.");
                return;
            }
            if (foot.IntersectsWith(pondZone)) { water = waterCap; Float("Air penuh", Color.LightSkyBlue, px - 20, py - 60); return; }
        }

        void SellAll()
        {
            int total = wood * 10 + stone * 12;
            for (int i = 0; i < 4; i++) total += crops[i] * Sell[i];
            if (total == 0) { Say("Tidak ada yang dijual."); return; }
            gold += total; wood = stone = 0; Array.Clear(crops);
            Float("+" + total + " emas", Color.Gold, px - 20, py - 70);
            if (gold >= GoalGold) won = true;
        }

        void Buy(int i)
        {
            int[] price = { 20, 35, 50, 90, 30, 300, 0, 0 };
            if (i < 4)
            {
                if (gold < SeedPrice[i]) { Say("Emas tidak cukup."); return; }
                gold -= SeedPrice[i]; seeds[i]++;
            }
            else if (i == 4) { if (gold < price[4]) { Say("Emas tidak cukup."); return; } gold -= 30; energy = 100; }
            else if (i == 5) { if (waterCap > 12) { Say("Sudah dimiliki."); return; } if (gold < 300) { Say("Emas tidak cukup."); return; } gold -= 300; waterCap = 30; water = 30; }
            else shopOpen = false;
        }

        void SleepNow()
        {
            foreach (var s in soil.Values)
            {
                if (s.Crop >= 0 && s.Watered && s.Age < Grow[s.Crop]) s.Age++;
                s.Watered = false;
            }
            day++; tm = 360; energy = 100; water = waterCap;
            Say("Hari ke-" + day + ". Selamat pagi!");
        }

        public void Update(float dt)
        {
            anim += dt;
            if (cooldown > 0) cooldown -= dt;
            if (msgT > 0) msgT -= dt;
            foreach (var f in floaters) { f.Life -= dt; f.Y -= 30 * dt; }
            floaters.RemoveAll(f => f.Life <= 0);
            if (shopOpen || won) return;

            tm += dt * 3f;
            if (tm >= 1440) { SleepNow(); Say("Kamu pingsan karena larut malam."); }

            int dx = 0, dy = 0;
            if (held.Contains(Keys.A) || held.Contains(Keys.Left)) dx--;
            if (held.Contains(Keys.D) || held.Contains(Keys.Right)) dx++;
            if (held.Contains(Keys.W) || held.Contains(Keys.Up)) dy--;
            if (held.Contains(Keys.S) || held.Contains(Keys.Down)) dy++;
            moving = dx != 0 || dy != 0;
            if (moving)
            {
                if (dx != 0 && dy != 0) { fdx = 0; fdy = dy; } else { fdx = dx; fdy = dy; }
                float len = MathF.Sqrt(dx * dx + dy * dy), sp = 190 * dt / len;
                float nx = px + dx * sp, ny = py + dy * sp;
                if (!Blocked(nx, py)) px = nx;
                if (!Blocked(px, ny)) py = ny;
                walk += dt * 10;
            }
        }

        // ---------- rendering ----------
        public void Render(Graphics g)
        {
            camX = Math.Clamp(px - vw / 2f, 0, Math.Max(0, WW - vw));
            camY = Math.Clamp(py - vh / 2f, 0, Math.Max(0, WH - vh));
            int cx = (int)camX, cy = (int)camY;
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(ground, new Rectangle(0, 0, Math.Min(vw, WW), Math.Min(vh, WH)), new Rectangle(cx, cy, Math.Min(vw, WW), Math.Min(vh, WH)), GraphicsUnit.Pixel);
            g.TranslateTransform(-cx, -cy);

            DrawPond(g);
            foreach (var kv in soil) DrawSoil(g, kv.Key, kv.Value);

            var tt = TargetTile();
            if (slot <= 1 || slot >= 4)
                using (var pen = new Pen(Color.FromArgb(170, 255, 255, 255), 2)) g.DrawRectangle(pen, tt.X * T, tt.Y * T, T, T);

            var draws = new List<(float y, Action a)>();
            foreach (var o in objs) { var oo = o; draws.Add((o.Y, () => g.DrawImageUnscaled(oo.Tree ? Sprites.Tree : Sprites.Rock, oo.Bounds.X, oo.Bounds.Y))); }
            draws.Add((shopRect.Bottom, () => g.DrawImageUnscaled(Sprites.Shop, shopRect.X, shopRect.Y)));
            draws.Add((binRect.Bottom, () => g.DrawImageUnscaled(Sprites.Crates, binRect.X, binRect.Y)));
            int fw = Sprites.Fence.Width, k = 0;
            for (int x = farmZone.X; x < farmZone.Right; x += fw, k++)
                if (x < 16 * T || x > 20 * T) { int xx = x; var img = k % 5 == 4 ? Sprites.Fence2 : Sprites.Fence; draws.Add((11 * T + 40, () => g.DrawImageUnscaled(img, xx, 11 * T))); }
            draws.Add((py, () => DrawPlayer(g)));
            foreach (var d in draws.OrderBy(d => d.y)) d.a();

            foreach (var f in floaters)
            {
                using var b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, f.Life)), f.C));
                g.DrawString(f.Text, fMed, Brushes.Black, f.X + 1, f.Y + 1);
                g.DrawString(f.Text, fMed, b, f.X, f.Y);
            }
            g.ResetTransform();

            int a = tm > 1080 ? (int)Math.Min(150, (tm - 1080) / 360f * 150) : tm < 420 ? (int)((420 - tm) / 60f * 90) : 0;
            if (a > 0) using (var b = new SolidBrush(Color.FromArgb(a, 10, 10, 60))) g.FillRectangle(b, 0, 0, vw, vh);

            DrawHud(g);
            if (shopOpen) DrawShop(g);
            if (won) Banner(g, "SELAMAT! Target " + GoalGold + " emas tercapai!", "Kamu petani sukses. Tutup jendela untuk keluar.");
        }

        void Rect(Graphics g, Color c, float x, float y, float w, float h) { using var b = new SolidBrush(c); g.FillRectangle(b, x, y, w, h); }

        void DrawPond(Graphics g)
        {
            Rect(g, Color.FromArgb(150, 120, 80), pond.X - 4, pond.Y - 4, pond.Width + 8, pond.Height + 8);
            using var path = new GraphicsPath(); path.AddEllipse(pond);
            using var b = new SolidBrush(Color.FromArgb(70, 140, 210)); g.FillPath(b, path);
            using var p = new Pen(Color.FromArgb(150, 210, 240), 2);
            for (int i = 0; i < 4; i++)
            {
                float ox = pond.X + 50 + i * 60 + MathF.Sin(anim * 2 + i) * 6, oy = pond.Y + 50 + (i % 2) * 60;
                g.DrawLine(p, ox, oy, ox + 22, oy);
            }
        }

        void DrawSoil(Graphics g, Point t, Soil s)
        {
            int x = t.X * T, y = t.Y * T;
            Rect(g, s.Watered ? Color.FromArgb(84, 56, 36) : Color.FromArgb(130, 90, 56), x + 1, y + 1, T - 2, T - 2);
            Rect(g, s.Watered ? Color.FromArgb(64, 42, 28) : Color.FromArgb(110, 74, 46), x + 4, y + 12, T - 8, 4);
            Rect(g, s.Watered ? Color.FromArgb(64, 42, 28) : Color.FromArgb(110, 74, 46), x + 4, y + 30, T - 8, 4);
            if (s.Crop >= 0) DrawCrop(g, s, x, y);
        }

        void DrawCrop(Graphics g, Soil s, int x, int y)
        {
            int c = s.Crop; float p = s.Age / (float)Grow[c];
            int cx = x + T / 2, by = y + T - 6;
            Color leaf = Color.FromArgb(50, 150, 50);
            if (p < 0.34f) { Rect(g, leaf, cx - 3, by - 8, 6, 8); Rect(g, leaf, cx - 8, by - 12, 5, 5); Rect(g, leaf, cx + 3, by - 12, 5, 5); return; }
            if (p < 1f) { Rect(g, leaf, cx - 2, by - 20, 4, 20); Rect(g, leaf, cx - 10, by - 22, 8, 6); Rect(g, leaf, cx + 2, by - 24, 8, 6); return; }
            Rect(g, leaf, cx - 2, by - 22, 4, 22); Rect(g, leaf, cx - 11, by - 24, 9, 6); Rect(g, leaf, cx + 2, by - 26, 9, 6);
            if (c == 0) { Rect(g, CropCol[0], cx - 5, by - 12, 10, 10); Rect(g, CropCol[0], cx - 3, by - 4, 6, 6); }
            else if (c == 1) { Rect(g, CropCol[1], cx - 11, by - 22, 8, 8); Rect(g, CropCol[1], cx + 4, by - 14, 8, 8); Rect(g, CropCol[1], cx - 4, by - 10, 8, 8); }
            else if (c == 2) { Rect(g, CropCol[2], cx - 4, by - 28, 8, 16); Rect(g, Color.ForestGreen, cx - 8, by - 20, 4, 10); Rect(g, Color.ForestGreen, cx + 4, by - 20, 4, 10); }
            else { Rect(g, CropCol[3], cx - 12, by - 16, 24, 16); Rect(g, Color.FromArgb(200, 100, 10), cx - 2, by - 16, 4, 16); }
        }

        void DrawPlayer(Graphics g)
        {
            const int U = 3;
            float x = px - 5 * U, y = py - 16 * U;
            float bob = moving ? MathF.Abs(MathF.Sin(walk)) * 2 : 0; y -= bob;
            float leg = moving ? MathF.Sin(walk) * 2 : 0;
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, px - 16, py - 6, 32, 12);
            Rect(g, Color.FromArgb(60, 50, 110), x + 1 * U, y + 12 * U + leg, 3 * U, 4 * U);
            Rect(g, Color.FromArgb(60, 50, 110), x + 6 * U, y + 12 * U - leg, 3 * U, 4 * U);
            Rect(g, Color.FromArgb(60, 130, 200), x, y + 6 * U, 10 * U, 6 * U);
            Rect(g, Color.FromArgb(240, 200, 160), x - 1 * U, y + 7 * U, 1 * U, 4 * U);
            Rect(g, Color.FromArgb(240, 200, 160), x + 10 * U, y + 7 * U, 1 * U, 4 * U);
            Rect(g, Color.FromArgb(240, 200, 160), x + 1 * U, y, 8 * U, 6 * U);
            Rect(g, Color.FromArgb(120, 70, 30), x + 1 * U, y - 1 * U, 8 * U, 2 * U);
            Rect(g, Color.FromArgb(220, 190, 80), x - 1 * U, y + 1 * U, 12 * U, 1 * U); // topi jerami
            if (fdy >= 0) { Rect(g, Color.Black, x + 3 * U, y + 3 * U, U, U); Rect(g, Color.Black, x + 6 * U, y + 3 * U, U, U); }
            if (fdx != 0) Rect(g, Color.FromArgb(150, 120, 60), px + fdx * 16, py - 24, 4, 14);
        }

        void DrawHud(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None;
            Rect(g, Color.FromArgb(190, 30, 25, 20), 10, 10, 250, 112);
            int h = (int)(tm / 60), m = (int)(tm % 60) / 10 * 10;
            g.DrawString($"Hari {day}   {h:00}:{m:00}", fMed, Brushes.White, 18, 14);
            g.DrawString($"Emas: {gold} / {GoalGold}", fMed, Brushes.Gold, 18, 38);
            g.DrawString("Energi", fSmall, Brushes.White, 18, 66);
            Rect(g, Color.FromArgb(60, 60, 60), 80, 68, 160, 12); Rect(g, Color.LimeGreen, 80, 68, 160 * energy / 100f, 12);
            g.DrawString("Air", fSmall, Brushes.White, 18, 88);
            Rect(g, Color.FromArgb(60, 60, 60), 80, 90, 160, 12); Rect(g, Color.DeepSkyBlue, 80, 90, 160 * water / waterCap, 12);

            int sw = 64, x0 = vw / 2 - sw * 4, y0 = vh - sw - 12;
            for (int i = 0; i < 8; i++)
            {
                int x = x0 + i * sw;
                Rect(g, i == slot ? Color.FromArgb(230, 255, 220, 100) : Color.FromArgb(190, 30, 25, 20), x, y0, sw - 4, sw - 4);
                Color[] ic = { Color.Gray, Color.DeepSkyBlue, Color.SaddleBrown, Color.DimGray, CropCol[0], CropCol[1], CropCol[2], CropCol[3] };
                Rect(g, ic[i], x + 20, y0 + 8, 20, 20);
                g.DrawString(SlotName[i], fSmall, i == slot ? Brushes.Black : Brushes.White, x + 2, y0 + 34);
                g.DrawString((i + 1).ToString(), fSmall, i == slot ? Brushes.Black : Brushes.Silver, x + 2, y0 + 1);
                if (i >= 4) g.DrawString("x" + seeds[i - 4], fSmall, i == slot ? Brushes.Black : Brushes.White, x + 40, y0 + 1);
            }
            string inv = $"Kayu {wood}  Batu {stone}  " + string.Join("  ", Enumerable.Range(0, 4).Select(i => CropName[i] + " " + crops[i]));
            g.DrawString(inv, fSmall, Brushes.White, x0, y0 - 20);
            g.DrawString("WASD gerak | Spasi/klik pakai alat | E interaksi | 1-8 / roda mouse alat | Z tidur", fSmall, Brushes.White, 14, vh - 22);
            if (msgT > 0) { var sz = g.MeasureString(msg, fMed); Rect(g, Color.FromArgb(200, 0, 0, 0), vw / 2 - sz.Width / 2 - 8, 20, sz.Width + 16, sz.Height + 6); g.DrawString(msg, fMed, Brushes.White, vw / 2 - sz.Width / 2, 23); }
        }

        void DrawShop(Graphics g)
        {
            int w = 420, h = 380, x = vw / 2 - w / 2, y = vh / 2 - h / 2;
            Rect(g, Color.FromArgb(235, 40, 30, 20), x, y, w, h);
            g.DrawString("Toko Bu Lastri", fMed, Brushes.Gold, x + 14, y + 10);
            g.DrawString("Emas: " + gold, fMed, Brushes.White, x + w - 120, y + 10);
            string[] names = { "Bibit Wortel", "Bibit Tomat", "Bibit Jagung", "Bibit Labu", "Pulihkan energi", "Kaleng besar", "", "Tutup" };
            string[] price = { "20", "35", "50", "90", "30", "300", "", "" };
            for (int i = 0; i < 8; i++)
            {
                if (i == 6) continue;
                int yy = y + 46 + i * 38;
                if (i == shopSel) Rect(g, Color.FromArgb(120, 255, 220, 100), x + 8, yy - 2, w - 16, 34);
                bool owned = i == 5 && waterCap > 12;
                g.DrawString(names[i], fMed, Brushes.White, x + 20, yy + 4);
                if (i < 6) g.DrawString(owned ? "Dimiliki" : price[i] + " emas", fMed, Brushes.Gold, x + w - 120, yy + 4);
            }
            g.DrawString("Atas/Bawah pilih | Enter beli | Esc tutup", fSmall, Brushes.Silver, x + 14, y + h - 24);
        }

        void Banner(Graphics g, string a, string b)
        {
            Rect(g, Color.FromArgb(200, 0, 0, 0), 0, vh / 2 - 70, vw, 140);
            var s1 = g.MeasureString(a, fBig); g.DrawString(a, fBig, Brushes.Gold, vw / 2 - s1.Width / 2, vh / 2 - 50);
            var s2 = g.MeasureString(b, fMed); g.DrawString(b, fMed, Brushes.White, vw / 2 - s2.Width / 2, vh / 2 + 10);
        }
    }
}
