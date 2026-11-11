using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm
{
    using Obj = WorldObject;

    public sealed partial class FarmGame
    {
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
            foreach (var o in objs)
            {
                var oo = o;
                if (oo.Kind == ObjKind.Chicken)
                {
                    float bob = MathF.Sin(anim * 4 + oo.X) * 2;
                    draws.Add((oo.Y, () => g.DrawImageUnscaled(Sprites.Chicken, oo.Bounds.X, oo.Bounds.Y + (int)bob)));
                }
                else
                {
                    var image = oo.Kind switch { ObjKind.Tree => Sprites.Tree, ObjKind.Rock => Sprites.Rock, _ => Sprites.House };
                    draws.Add((oo.Y, () => g.DrawImageUnscaled(image, oo.Bounds.X, oo.Bounds.Y)));
                }
            }
            draws.Add((shopRect.Bottom, () => g.DrawImageUnscaled(Sprites.Shop, shopRect.X, shopRect.Y)));
            draws.Add((binRect.Bottom, () => g.DrawImageUnscaled(Sprites.Crates, binRect.X, binRect.Y)));
            int fh = Sprites.Fence.Height, fx = farmZone.X;
            draws.Add((FenceTopY + fh, () => g.DrawImageUnscaled(Sprites.Fence, fx, FenceTopY)));
            // bottom fence is split around the path gap (x 96..192 of the sprite is left open)
            draws.Add((FenceBottomY + fh, () =>
            {
                g.DrawImage(Sprites.Fence, new Rectangle(fx, FenceBottomY, 96, fh), new Rectangle(0, 0, 96, fh), GraphicsUnit.Pixel);
                int rw = Sprites.Fence.Width - 192;
                g.DrawImage(Sprites.Fence, new Rectangle(fx + 192, FenceBottomY, rw, fh), new Rectangle(192, 0, rw, fh), GraphicsUnit.Pixel);
            }));
            draws.Add((py, () => DrawPlayer(g)));
            foreach (var d in draws.OrderBy(d => d.y)) d.a();

            foreach (var f in floaters)
            {
                using var b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, f.Life)), f.C));
                g.DrawString(f.Text, fMed, Brushes.Black, f.X + 1, f.Y + 1);
                g.DrawString(f.Text, fMed, b, f.X, f.Y);
            }
            g.ResetTransform();

            DrawNight(g);

            DrawHud(g);
            if (shopOpen) DrawShop(g);
            if (won) Banner(g, "SELAMAT! Target " + GoalGold + " emas tercapai!", "Kamu petani sukses dalam " + day + " hari.");
            else if (paused) DrawPause(g);
        }

        void Rect(Graphics g, Color c, float x, float y, float w, float h)
        {
            using var b = new SolidBrush(c);
            g.FillRectangle(b, x, y, w, h);
        }

        void DrawPond(Graphics g)
        {
            g.DrawImageUnscaled(Sprites.Pond, pond.X, pond.Y);
        }

        void DrawSoil(Graphics g, Point t, Soil s)
        {
            int x = t.X * T, y = t.Y * T;
            g.DrawImageUnscaled(Sprites.SoilTilled, x, y);
            if (s.Watered) Rect(g, Color.FromArgb(90, 20, 14, 8), x, y, T, T);
            if (s.Crop >= 0) DrawCrop(g, s, x, y);
        }

        void DrawCrop(Graphics g, Soil s, int x, int y)
        {
            int c = s.Crop;
            float p = s.Age / (float)Grow[c];
            int cx = x + T / 2, by = y + T - 8;
            if (p < 0.34f) { g.DrawImageUnscaled(Sprites.SproutS, cx - Sprites.SproutS.Width / 2, by - Sprites.SproutS.Height); return; }
            if (p < 1f) { g.DrawImageUnscaled(Sprites.SproutM, cx - Sprites.SproutM.Width / 2, by - Sprites.SproutM.Height); return; }
            g.DrawImageUnscaled(Sprites.Harvest, cx - Sprites.Harvest.Width / 2, by - Sprites.Harvest.Height);
            using var b = new SolidBrush(CropCol[c]);
            g.FillEllipse(b, cx - 5, y + T - 10, 10, 10);
        }

        void DrawPlayer(Graphics g)
        {
            bool run = moving && (held.Contains(Keys.ShiftKey));
            int frame = ((int)(walk / 2f)) & 1;
            Bitmap img;
            bool flip = false;
            if (fdx != 0) { img = run ? Sprites.PRun : Sprites.PRight; flip = fdx < 0; }
            else if (fdy < 0) { img = Sprites.PBack; flip = moving && frame == 1; }
            else img = !moving ? Sprites.PFront : (frame == 0 ? Sprites.PDown1 : Sprites.PDown2);

            float bob = moving ? MathF.Abs(MathF.Sin(walk)) * 3 : 0;
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, px - 18, py - 7, 36, 14);
            float x = px - img.Width / 2f, y = py - img.Height + 6 - bob;
            if (flip) g.DrawImage(img, new PointF[] { new(x + img.Width, y), new(x, y), new(x + img.Width, y + img.Height) });
            else g.DrawImageUnscaled(img, (int)x, (int)y);
        }
    }
}

