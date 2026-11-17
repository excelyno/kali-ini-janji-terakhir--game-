using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using kali_ini_janji_terakhir.Core.Entities.Props;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Farm.Ui;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        private const float MouseAimSeconds = 3f;   // mouse dipakai untuk membidik selama 3 detik setelah digerakkan
        private Point lastMouse;
        private float mouseAimTimer;
        private bool waitForRelease;   // setelah menutup jendela dengan Spasi/klik, jangan langsung mengayun alat

        /// <summary>Jendela terbuka: semua tombol yang baru ditekan diteruskan ke jendela teratas.</summary>
        private void HandleWindowInput(UiWindow top)
        {
            foreach (var key in Input.PressedKeys) top.HandleKey(key, world);
            if (Input.LeftClicked) top.Click(world);
            waitForRelease = true;
        }

        private void HandleGameplayInput(float dt)
        {
            var player = world.Player;

            // ---- menu dan tombol sekali tekan
            if (Input.WasPressed(Keys.Escape, Keys.P)) { OpenPause(); return; }
            if (Input.WasPressed(Keys.Q, Keys.Tab)) { Open(new JournalWindow()); return; }
            if (Input.WasPressed(Keys.F3)) view.ShowDebug = !view.ShowDebug;
            if (Input.WasPressed(Keys.F5)) { Save(); world.Say("Permainan disimpan."); }
            if (Input.WasPressed(Keys.Z)) TrySleep();

            int slot = Input.PressedNumber();
            if (slot >= 0) player.ActiveSlot = slot;
            if (Input.WheelSteps != 0) player.ActiveSlot += Input.WheelSteps;

            // ---- bidikan: mouse (jika baru digerakkan) atau arah hadap
            if (Input.Mouse != lastMouse) { lastMouse = Input.Mouse; mouseAimTimer = MouseAimSeconds; }
            mouseAimTimer -= dt;
            player.Aim(mouseAimTimer > 0 ? MouseTile() : null);

            // ---- aksi: Spasi/klik kiri = alat (tahan untuk mengulang), E/klik kanan = interaksi
            if (Input.WasPressed(Keys.E) || Input.RightClicked) world.FindInteractable()?.Interact(world);
            bool toolHeld = Input.IsHeld(Keys.Space) || Input.LeftHeld;
            if (!toolHeld) waitForRelease = false;
            else if (!waitForRelease) player.UseActiveTool(world);

            // ---- gerak
            var (dx, dy) = Input.MoveAxis;
            player.Control(dx, dy, Input.IsHeld(Keys.ShiftKey), dt, world);
        }

        private Point MouseTile()
        {
            var p = camera.ScreenToWorld(Input.Mouse);
            return TileMap.TileAt(p.X, p.Y);
        }

        private void TrySleep()
        {
            var house = world.Objects.OfType<House>().FirstOrDefault();
            if (house != null && house.InRange(world)) house.Interact(world);
            else world.Say("Tidur hanya bisa di depan pintu rumah.");
        }

        private void OpenPause() => Open(new PauseWindow(Save, () => ExitRequested = true));
    }
}
