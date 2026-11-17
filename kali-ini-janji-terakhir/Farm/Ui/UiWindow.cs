using System.Windows.Forms;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Farm.Ui
{
    /// <summary>
    /// ABSTRAKSI: jendela di atas permainan (dialog, toko, jurnal, jeda, akhir).
    /// FarmGame menyimpan tumpukan UiWindow dan hanya memanggil HandleKey/Update/Draw
    /// milik jendela paling atas. Setiap jendela mengisi perilakunya sendiri: POLIMORFISME.
    /// </summary>
    public abstract class UiWindow
    {
        public bool IsClosed { get; private set; }

        protected void Close() => IsClosed = true;

        /// <summary>Apakah dunia berhenti (waktu tidak jalan) selama jendela terbuka.</summary>
        public virtual bool PausesWorld => true;

        public virtual void Update(float dt) { }

        public abstract void HandleKey(Keys key, FarmWorld world);

        /// <summary>Klik mouse; default sama seperti menekan Enter.</summary>
        public virtual void Click(FarmWorld world) => HandleKey(Keys.Enter, world);

        public abstract void Draw(Renderer r, int vw, int vh, FarmWorld world);

        protected static bool IsUp(Keys k) => k is Keys.Up or Keys.W;
        protected static bool IsDown(Keys k) => k is Keys.Down or Keys.S;
        protected static bool IsConfirm(Keys k) => k is Keys.Enter or Keys.Space or Keys.E;
        protected static bool IsBack(Keys k) => k is Keys.Escape;
    }

    /// <summary>
    /// Jendela berbentuk daftar pilihan. PEWARISAN: UiWindow → MenuWindow → (Shop, Pause, ...).
    /// Navigasi atas/bawah ditulis sekali di sini.
    /// </summary>
    public abstract class MenuWindow : UiWindow
    {
        private int selected;

        protected int Selected => selected;
        protected abstract int OptionCount { get; }

        protected abstract void Choose(int index, FarmWorld world);
        protected virtual void Cancel(FarmWorld world) => Close();

        public override void HandleKey(Keys key, FarmWorld world)
        {
            int n = OptionCount;
            if (n == 0) { if (IsBack(key) || IsConfirm(key)) Cancel(world); return; }
            if (IsUp(key)) selected = (selected + n - 1) % n;
            else if (IsDown(key)) selected = (selected + 1) % n;
            else if (IsConfirm(key)) Choose(selected, world);
            else if (IsBack(key)) Cancel(world);
        }
    }
}
