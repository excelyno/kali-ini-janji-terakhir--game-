using System;
using System.Drawing;
using System.Linq;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Core.Tools;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Farm.Ui;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Menggambar dunia dari sudut pandang kamera, berlapis dari bawah ke atas:
    ///   1. tanah (bitmap cache dari TileMap)
    ///   2. petak cangkul (autotile, basah/kering)
    ///   3. kursor tile
    ///   4. benda + tanaman, diurutkan menurut Y kaki (Y-sorting / painter's algorithm)
    ///   5. partikel dan teks melayang
    ///   6. cuaca dan warna langit (layar)
    /// </summary>
    public sealed class WorldRenderer : IDisposable
    {
        private readonly FarmWorld world;
        private readonly Bitmap ground;

        public WorldRenderer(FarmWorld world)
        {
            this.world = world;
            ground = world.Map.RenderGround();
        }

        /// <summary>F3: tampilkan kotak tabrakan (hitbox) dan tile sasaran.</summary>
        public bool ShowDebug { get; set; }

        public void Draw(Renderer r, Camera2D camera, bool showCursor)
        {
            var view = camera.View;
            r.BeginScreen();
            r.G.Clear(Color.FromArgb(40, 60, 30));
            r.BeginWorld(camera);

            var visible = Rectangle.Intersect(Rectangle.Round(view), new Rectangle(0, 0, ground.Width, ground.Height));
            r.G.DrawImage(ground, visible, visible, GraphicsUnit.Pixel);
            DrawSoil(r, view);
            if (showCursor) DrawTileCursor(r);

            var cull = RectangleF.Inflate(view, 96, 160);
            var sorted = world.Objects.Where(o => o.Bounds.IntersectsWith(cull)).Cast<IWorldDrawable>()
                .Concat(world.Plots.Where(p => p.Crop != null && p.Bounds.IntersectsWith(cull)))
                .OrderBy(d => d.SortY);
            foreach (var d in sorted) d.Draw(r, world.Time);

            world.Effects.Draw(r);
            foreach (var f in world.Floaters)
            {
                float w = r.Measure(f.Text, Fonts.Body).Width;
                r.Text(f.Text, Fonts.Body, f.Color, f.X - w / 2, f.Y, alpha: f.Alpha);
            }
            if (ShowDebug) DrawDebug(r, cull);

            r.BeginScreen();
            var screen = new Rectangle(0, 0, camera.ViewWidth, camera.ViewHeight);
            DrawWeather(r, camera, screen);
            r.Tint(world.Clock.SkyOverlay(), screen);
        }

        /// <summary>Petak cangkul tersambung rapi: mask dari petak tetangga, sprite basah bila disiram.</summary>
        private void DrawSoil(Renderer r, RectangleF view)
        {
            foreach (var plot in world.Plots)
            {
                if (!plot.Area.IntersectsWith(Rectangle.Round(view))) continue;
                int mask = TileMap.NeighborMask(plot.Tile, t => world.PlotAt(t) != null);
                r.Draw($"{(plot.IsWatered ? "tilled_wet" : "tilled")}_{mask}", plot.Area.X, plot.Area.Y);
            }
        }

        /// <summary>Kotak sasaran ala Stardew: putih = alat berguna di sini, merah = tidak.</summary>
        private void DrawTileCursor(Renderer r)
        {
            var p = world.Player;
            var tool = p.ActiveTool;
            var tile = p.TargetTile;
            if (!world.Map.InBounds(tile)) return;
            bool useful = tool.IsUsefulAt(world, p, tile);
            if (!useful && tool is StrikeTool) return;   // kapak/beliung: hanya tampil saat menunjuk pohon/batu
            var rect = TileMap.TileRect(tile);
            r.Draw(useful ? "ui_cursor_ok" : "ui_cursor_bad", rect.X, rect.Y);
        }

        private void DrawWeather(Renderer r, Camera2D camera, Rectangle screen)
        {
            float t = world.Time;
            switch (world.Weather)
            {
                case WeatherKind.Berawan:
                    r.Tint(Color.FromArgb(40, 60, 70, 90), screen);
                    break;
                case WeatherKind.Hujan:
                    r.Tint(Color.FromArgb(60, 40, 50, 80), screen);
                    var drop = Art.Get("fx_rain");
                    for (int i = 0; i < 180; i++)
                    {
                        float x = ((i * 97.3f - camera.X * 0.3f) % screen.Width + screen.Width) % screen.Width;
                        float y = (i * 53.7f + t * 700) % (screen.Height + 40) - 20;
                        r.Draw(drop, x - (y * 0.25f) % 40, y);
                    }
                    break;
                case WeatherKind.Salju:
                    r.Tint(Color.FromArgb(50, 220, 230, 255), screen);
                    var flake = Art.Get("fx_snow");
                    for (int i = 0; i < 120; i++)
                    {
                        float x = ((i * 131.1f + MathF.Sin(t + i) * 20 - camera.X * 0.3f) % screen.Width + screen.Width) % screen.Width;
                        float y = (i * 71.3f + t * 60) % screen.Height;
                        r.Draw(flake, x, y);
                    }
                    break;
            }
        }

        /// <summary>Alat bantu developer: garis hitbox (merah) dan sasaran pemain (kuning).</summary>
        private void DrawDebug(Renderer r, RectangleF cull)
        {
            using var red = new Pen(Color.FromArgb(220, 255, 40, 40), 2);
            using var yellow = new Pen(Color.Yellow, 2);
            foreach (var o in world.Objects)
                if (!o.Hitbox.IsEmpty && o.Hitbox.IntersectsWith(cull))
                    r.G.DrawRectangle(red, o.Hitbox.X, o.Hitbox.Y, o.Hitbox.Width, o.Hitbox.Height);
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                    if (world.Map.IsSolid(new Point(x, y)))
                        r.G.DrawRectangle(red, TileMap.TileRect(new Point(x, y)));
            r.G.DrawRectangle(yellow, TileMap.TileRect(world.Player.TargetTile));
        }

        public void Dispose() => ground.Dispose();
    }
}
