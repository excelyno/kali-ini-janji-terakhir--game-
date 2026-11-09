using System.Diagnostics;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Forms
{
    public class GamePanel : Panel
    {
        private readonly FarmGame game;
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
        private readonly Stopwatch sw = Stopwatch.StartNew();
        private double last;

        public GamePanel()
        {
            game = new FarmGame();
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            DoubleBuffered = true;
            timer.Tick += (_, _) =>
            {
                double now = sw.Elapsed.TotalSeconds;
                game.Update((float)Math.Min(0.05, now - last));
                last = now;
                Invalidate();
            };
            timer.Start();
        }

        public void KeyDownEvt(Keys k) => game.KeyDown(k);
        public void KeyUpEvt(Keys k) => game.KeyUp(k);
        public void WheelEvt(int d) => game.Wheel(d);
        public void LoseFocusEvt() => game.LoseFocus();

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); game.Click(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); game.Resize(ClientSize.Width, ClientSize.Height); }
        protected override void OnPaint(PaintEventArgs e) => game.Render(e.Graphics);

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); game.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
