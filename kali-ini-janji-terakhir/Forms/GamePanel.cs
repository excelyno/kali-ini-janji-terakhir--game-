using System.Diagnostics;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Forms
{
    /// <summary>
    /// Kanvas permainan dan game loop.
    /// Memakai "fixed timestep": logika selalu maju 1/60 detik per langkah, berapa pun FPS layar,
    /// sehingga gerak dan tabrakan konsisten di komputer cepat maupun lambat.
    /// </summary>
    public class GamePanel : Panel
    {
        private const double Step = 1.0 / 60.0;
        private const int MaxStepsPerTick = 5;   // jangan "mengejar" terlalu jauh setelah jeda panjang

        private FarmGame? game;
        private HomeScreen? home;
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double last, accumulator;

        public GamePanel()
        {
            Art.Load();
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            DoubleBuffered = true;
            home = new HomeScreen();
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            accumulator = Math.Min(accumulator + now - last, Step * MaxStepsPerTick);
            last = now;

            while (accumulator >= Step)
            {
                accumulator -= Step;
                if (!StepOnce((float)Step)) return;
            }
            Invalidate();
        }

        /// <summary>Satu langkah logika. false = jendela ditutup.</summary>
        private bool StepOnce(float dt)
        {
            if (home != null)
            {
                home.Update(dt);
                switch (home.Action)
                {
                    case HomeAction.NewGame: StartGame(false); break;
                    case HomeAction.Continue: StartGame(true); break;
                    case HomeAction.Quit: timer.Stop(); FindForm()?.Close(); return false;
                }
                return true;
            }
            if (game == null) return true;
            if (game.ExitRequested) { ShowHome(); return true; }
            if (game.RestartRequested) StartGame(false);
            game!.Update(dt);
            return true;
        }

        private void StartGame(bool load)
        {
            home?.Dispose();
            home = null;
            game?.Dispose();
            game = new FarmGame(load);
            game.Resize(ClientSize.Width, ClientSize.Height);
        }

        private void ShowHome()
        {
            game?.Dispose();
            game = null;
            home = new HomeScreen();
            home.Resize(ClientSize.Width, ClientSize.Height);
        }

        // ---------------------------------------------------------------- input dari GameForm / panel
        public void KeyDownEvt(Keys k)
        {
            if (home != null) home.KeyDown(k);
            else game?.Input.KeyDown(k);
        }

        public void KeyUpEvt(Keys k) => game?.Input.KeyUp(k);
        public void WheelEvt(int delta) => game?.Input.Wheel(delta);
        public void LoseFocusEvt() => game?.LoseFocus();

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            home?.MouseMove(e.X, e.Y);
            game?.Input.MouseMove(e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (home != null) home.Click(e.X, e.Y);
            else game?.Input.MouseDown(e.Button);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            game?.Input.MouseUp(e.Button);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            home?.Resize(ClientSize.Width, ClientSize.Height);
            game?.Resize(ClientSize.Width, ClientSize.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (home != null) home.Render(e.Graphics);
            else game?.Render(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                game?.Dispose();
                home?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
