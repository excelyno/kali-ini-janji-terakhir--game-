using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace kali_ini_janji_terakhir.Farm
{
    using Obj = WorldObject;

    public sealed partial class FarmGame
    {
        const int T = 48, MW = 40, MH = 30, WW = MW * T, WH = MH * T;
        const int FenceTopY = 350, FenceBottomY = 13 * T + 2;

        void InitializeSolids()
        {
            solids.Add(new Rectangle(shopRect.X, shopRect.Y + shopRect.Height / 2, shopRect.Width, shopRect.Height / 2));
            solids.Add(new Rectangle(binRect.X, binRect.Bottom - 20, binRect.Width, 20));
            solids.Add(new Rectangle(pond.X + pond.Width * 22 / 100, pond.Y + pond.Height * 6 / 100, pond.Width * 72 / 100, pond.Height * 88 / 100));
            // fence: top is closed, bottom has a gap where the main path enters the field
            solids.Add(new Rectangle(farmZone.X, FenceTopY + 12, farmZone.Width, 14));
            solids.Add(new Rectangle(farmZone.X, FenceBottomY + 8, 96, 12));
            solids.Add(new Rectangle(farmZone.X + 192, FenceBottomY + 8, farmZone.Width - 192, 12));
        }

        void InitializeWorldObjects()
        {
            // trees / rocks / chickens placed like the goal map
            foreach (var (x, y) in new[] { (347, 488), (373, 369), (1504, 358), (1584, 488), (358, 929), (677, 981), (1421, 970), (1530, 885) }) AddObj(true, x, y);
            foreach (var (x, y) in new[] { (386, 586), (731, 785), (1068, 835), (1135, 907) }) AddObj(false, x, y);

            // filler forest outside of the goal area
            var rnd = new Random(7);
            for (int i = 0; i < 60; i++)
            {
                float x = rnd.Next(60, WW - 60), y = rnd.Next(200, WH - 40);
                if (x < 1700 && y < 1290) continue;
                if (objs.Any(o => Math.Abs(o.X - x) < 90 && Math.Abs(o.Y - y) < 60)) continue;
                AddObj(i % 2 == 0, x, y);
            }
            for (int x = 30; x < WW; x += 130) { AddObj(true, x, 150); AddObj(true, x + 60, WH - 10); }
            for (int y = 300; y < WH - 100; y += 160) { AddObj(true, 24, y); AddObj(true, WW - 24, y); }
            AddObj(ObjKind.House, houseRect.X + houseRect.Width / 2, houseRect.Bottom);
            AddObj(ObjKind.Chicken, 662, 582);
            AddObj(ObjKind.Chicken, 751, 553);
            AddObj(ObjKind.Chicken, 1091, 753);
        }

        void InitializeStartingSoil()
        {
            // two rows of the field start already tilled, as in the goal picture
            for (int tx = farmZone.X / T; tx < farmZone.Right / T; tx++)
                for (int ty = farmZone.Y / T; ty < farmZone.Y / T + 2; ty++)
                    soil[new Point(tx, ty)] = new Soil();
        }

        void AddObj(bool tree, float x, float y) => AddObj(tree ? ObjKind.Tree : ObjKind.Rock, x, y);

        void AddObj(ObjKind kind, float x, float y)
        {
            var o = new WorldObject { Id = nextId++, Kind = kind, X = x, Y = y };
            var s = kind switch { ObjKind.Tree => Sprites.Tree, ObjKind.Rock => Sprites.Rock, ObjKind.House => Sprites.House, _ => Sprites.Chicken };
            o.Bounds = new Rectangle((int)x - s.Width / 2, (int)y - s.Height, s.Width, s.Height);
            o.Solid = kind == ObjKind.Tree ? new Rectangle((int)x - 14, (int)y - 14, 28, 14) : kind == ObjKind.Rock ? new Rectangle((int)x - s.Width / 2 + 6, (int)y - s.Height / 2, s.Width - 12, s.Height / 2) : kind == ObjKind.House ? new Rectangle((int)x - s.Width / 2 + 20, (int)y - 55, s.Width - 40, 45) : Rectangle.Empty;
            objs.Add(o);
            if (o.Solid != Rectangle.Empty) solids.Add(o.Solid);
        }

        Bitmap BuildGround()
        {
            var bmp = new Bitmap(WW, WH, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(142, 172, 104));
            var r = new Random(3);
            Color[] gc = { Color.FromArgb(136, 166, 98), Color.FromArgb(148, 178, 110), Color.FromArgb(130, 160, 94) };
            for (int i = 0; i < 6000; i++)
            {
                using var b = new SolidBrush(gc[r.Next(3)]);
                g.FillRectangle(b, r.Next(WW), r.Next(WH), 6, 6);
            }
            using var path = new SolidBrush(Color.FromArgb(200, 175, 120));
            g.FillRectangle(path, 560, 488, 66, 246);        // from the front door down
            g.FillRectangle(path, 434, 727, 574, 59);        // east-west path past the shop
            g.FillRectangle(path, 912, 624, 96, 602);        // main path through the field gap
            g.FillRectangle(path, 912, 914, 656, 61);        // path towards the east
            for (int tx = farmZone.X; tx < farmZone.Right; tx += T)
                for (int ty = farmZone.Y; ty < farmZone.Bottom; ty += T)
                    g.DrawImageUnscaled(Sprites.SoilBare, tx, ty);
            return bmp;
        }

        bool Blocked(float x, float y)
        {
            var r = new Rectangle((int)x - 12, (int)y - 12, 24, 12);
            if (x < 20 || x > WW - 20 || y < 40 || y > WH - 10) return true;
            foreach (var s in solids) if (s.IntersectsWith(r)) return true;
            return false;
        }

        Point TargetPoint() => new((int)(px + fdx * 36), (int)(py - 10 + fdy * 36));
        Point TargetTile() { var p = TargetPoint(); return new Point(Math.Clamp(p.X / T, 0, MW - 1), Math.Clamp(p.Y / T, 0, MH - 1)); }
    }
}

