using System.Diagnostics;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Forms
{
    public class GamePanel : Panel
    {
        private FarmGame? game;
        private HomeScreen? home;
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
        private readonly Stopwatch sw = Stopwatch.StartNew();
        private double last;

        public GamePanel()
        {
            Sprites.Load();
            home = new HomeScreen();
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            DoubleBuffered = true;
            timer.Tick += (_, _) =>
            {
                double now = sw.Elapsed.TotalSeconds;
                float dt = (float)Math.Min(0.05, now - last);
                last = now;

                if (home != null)
                {
                    home.Update(dt);
                    switch (home.Action)
                    {
                        case HomeAction.NewGame: StartGame(false); break;
                        case HomeAction.Continue: StartGame(true); break;
                        case HomeAction.Quit: timer.Stop(); FindForm()?.Close(); return;
                    }
                }
                else if (game != null)
                {
                    if (game.ExitRequested) { ShowHome(); }
                    else
                    {
                        if (game.RestartRequested) { game.Dispose(); game = new FarmGame(); game.Resize(ClientSize.Width, ClientSize.Height); }
                        game.Update(dt);
                    }
                }
                Invalidate();
            };
            timer.Start();
        }

        private void StartGame(bool load)
        {
            home?.Dispose(); home = null;
            game = new FarmGame(load);
            game.Resize(ClientSize.Width, ClientSize.Height);
        }

        private void ShowHome()
        {
            game?.Dispose(); game = null;
            home = new HomeScreen();
            home.Resize(ClientSize.Width, ClientSize.Height);
        }

        public void KeyDownEvt(Keys k) { if (home != null) home.KeyDown(k); else game?.KeyDown(k); }
        public void KeyUpEvt(Keys k) => game?.KeyUp(k);
        public void WheelEvt(int d) => game?.Wheel(d);
        public void LoseFocusEvt() => game?.LoseFocus();

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); home?.MouseMove(e.X, e.Y); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (home != null) home.Click(e.X, e.Y); else game?.Click();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            home?.Resize(ClientSize.Width, ClientSize.Height);
            game?.Resize(ClientSize.Width, ClientSize.Height);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (home != null) home.Render(e.Graphics); else game?.Render(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); game?.Dispose(); home?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
