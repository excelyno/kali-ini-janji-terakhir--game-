using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using kali_ini_janji_terakhir.Core.Entities.Npcs;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Farm.Ui;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Pengendali permainan: menghubungkan input, model dunia (FarmWorld), kamera, dan tumpukan
    /// jendela UI. Dibagi ke beberapa file partial:
    /// - FarmGame.Input.cs  (membaca keyboard/mouse dan menjalankan aksi)
    /// - FarmGame.Render.cs (gambar dunia, HUD, jendela)
    /// - FarmGame.Save.cs   (simpan / muat)
    /// </summary>
    public sealed partial class FarmGame : IDisposable
    {
        private readonly FarmWorld world = new();
        private readonly WorldRenderer view;
        private readonly Camera2D camera = new();
        private readonly List<UiWindow> windows = new();

        public FarmGame(bool loadSave = false)
        {
            // Dunia meminta jendela lewat event; FarmGame yang memutuskan cara menampilkannya.
            world.DialogueRequested += (d, npc) => Open(new DialogWindow(d, npc));
            world.ShopRequested += () => Open(new ShopWindow(world));
            world.SaveRequested += Save;
            world.EndingRequested += () =>
            {
                SaveSystem.DeleteSave();
                Open(new EndingWindow(() => RestartRequested = true, () => ExitRequested = true));
            };

            if (loadSave) ApplySave();
            else world.Say("Selamat datang! Temui Kakek Wira (tanda !) di jalan dekat ladang.");

            view = new WorldRenderer(world);
            camera.SnapTo(CameraTarget, world.Map.PixelSize);
        }

        /// <summary>Input dari GamePanel. Dibaca sekali per update lalu dibersihkan.</summary>
        public InputState Input { get; } = new();

        public bool RestartRequested { get; private set; }
        public bool ExitRequested { get; private set; }

        /// <summary>Dipakai alat developer (screenshot) untuk menyiapkan keadaan.</summary>
        internal FarmWorld World => world;

        private UiWindow? TopWindow => windows.LastOrDefault(w => !w.IsClosed);
        private PointF CameraTarget => new(world.Player.X, world.Player.Y - 40);

        private void Open(UiWindow w)
        {
            windows.Add(w);
            world.Player.StopMoving();
        }

        /// <summary>Satu langkah simulasi (dipanggil 60x per detik oleh game loop).</summary>
        public void Update(float dt)
        {
            foreach (var w in windows.ToList()) w.Update(dt);
            windows.RemoveAll(w => w.IsClosed);

            if (TopWindow is { } top) HandleWindowInput(top);
            else HandleGameplayInput(dt);

            bool paused = windows.Any(w => w.PausesWorld);
            world.Update(dt, simulate: !paused);
            camera.Follow(CameraTarget, world.Map.PixelSize, dt);
            Input.EndFrame();
        }

        public void Resize(int w, int h) => camera.Resize(w, h);

        public void LoseFocus()
        {
            Input.Clear();
            world.Player.StopMoving();
            if (TopWindow == null) OpenPause();
        }

        public void Dispose() => view.Dispose();
    }
}
