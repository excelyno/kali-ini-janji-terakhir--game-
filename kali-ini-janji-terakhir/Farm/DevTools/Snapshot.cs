using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Entities.Npcs;
using kali_ini_janji_terakhir.Forms;

namespace kali_ini_janji_terakhir.Farm.DevTools
{
    /// <summary>
    /// Alat developer: "--snapshot out.png" merender beberapa adegan ke file PNG tanpa membuka jendela.
    /// Berguna untuk memeriksa tampilan (posisi sprite, hitbox, HUD) dengan cepat.
    /// Menghasilkan out_home, out_game, out_debug, out_walk, out_swing, out_dialog, out_shop, out_journal (.png).
    /// </summary>
    internal static class Snapshot
    {
        private const int W = 1280, H = 720;

        public static void Run(string path)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            string name = Path.GetFileNameWithoutExtension(path);
            string Out(string suffix) => Path.Combine(dir, $"{name}_{suffix}.png");

            using (var home = new HomeScreen())
            {
                home.Resize(W, H);
                for (int i = 0; i < 60; i++) home.Update(1 / 60f);
                Save(Out("home"), home.Render);
            }

            using var game = new FarmGame();
            game.Resize(W, H);
            var world = game.World;
            PlantDemoCrops(world);
            Step(game, 90);
            Save(Out("game"), game.Render);

            Press(game, Keys.F3);
            Save(Out("debug"), game.Render);
            Press(game, Keys.F3);

            // jalan ke kanan bawah sampai menabrak sesuatu (uji collision)
            Hold(game, Keys.D, 120);
            Hold(game, Keys.S, 60);
            Save(Out("walk"), game.Render);

            // ayun kapak ke pohon: cek posisi tangan, partikel serpihan, kursor
            var tree = world.Objects.OfType<Core.Entities.Props.Tree>().First(t => !t.IsBorder);
            world.Player.Teleport(tree.X - 56, tree.Y + 2);
            game.Input.MouseMove(new Point(-1, -1));
            Press(game, Keys.D3);
            Hold(game, Keys.D, 6);
            game.Input.KeyDown(Keys.Space);
            Step(game, 9);
            game.Input.KeyUp(Keys.Space);
            Save(Out("swing"), game.Render);
            Step(game, 60);

            var wira = world.Objects.OfType<Wira>().First();
            wira.Interact(world);
            Step(game, 120);
            Save(Out("dialog"), game.Render);
            for (int i = 0; i < 12; i++) Press(game, Keys.Enter);   // lewati halaman + pilih jawaban pertama

            world.OpenShop();
            Step(game, 5);
            Save(Out("shop"), game.Render);
            for (int i = 0; i < 3; i++) Press(game, Keys.Escape);
            world.Player.Inventory.Add(Core.Items.ItemDb.Egg, 3);
            Press(game, Keys.Q);
            Save(Out("journal"), game.Render);
            Console.WriteLine("Snapshot disimpan di " + dir);
        }

        /// <summary>Tanam semua jenis tanaman di semua tahap supaya sprite tanaman terlihat.</summary>
        private static void PlantDemoCrops(FarmWorld world)
        {
            var plots = world.Plots.OrderBy(p => p.Tile.Y).ThenBy(p => p.Tile.X).ToList();
            var kinds = CropFactory.All.ToList();
            for (int i = 0; i < plots.Count && i < kinds.Count * 4; i++)
            {
                var crop = CropFactory.Create(kinds[i / 4]);
                crop.RestoreAge(i % 4 * crop.GrowDays / 3);
                plots[i].Plant(crop);
                if (i % 2 == 0) plots[i].Water();
            }
        }

        private static void Step(FarmGame game, int frames)
        {
            for (int i = 0; i < frames; i++) game.Update(1 / 60f);
        }

        private static void Press(FarmGame game, Keys key)
        {
            game.Input.KeyDown(key);
            game.Update(1 / 60f);
            game.Input.KeyUp(key);
            game.Update(1 / 60f);
        }

        private static void Hold(FarmGame game, Keys key, int frames)
        {
            game.Input.KeyDown(key);
            Step(game, frames);
            game.Input.KeyUp(key);
            game.Update(1 / 60f);
        }

        private static void Save(string file, Action<Graphics> render)
        {
            using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) render(g);
            bmp.Save(file, ImageFormat.Png);
        }
    }
}
