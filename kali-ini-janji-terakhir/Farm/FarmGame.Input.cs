using System;
using System.Windows.Forms;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        public void Resize(int w, int h)
        {
            vw = Math.Max(100, w);
            vh = Math.Max(100, h);
        }

        public void LoseFocus()
        {
            held.Clear();
            if (started && !won) paused = true;
        }

        public void KeyUp(Keys k) => held.Remove(k);

        public void Wheel(int delta)
        {
            if (started && !shopOpen && !paused)
                slot = (slot + (delta > 0 ? 7 : 1)) % 8;
        }

        public void Click()
        {
            if (!started)
            {
                started = true;
                held.Clear();
                return;
            }
            if (!shopOpen && !paused && !won)
                UseTool();
        }

        public void KeyDown(Keys k)
        {
            bool fresh = held.Add(k);
            if (!fresh) return;

            if (won)
            {
                if (k == Keys.R || k == Keys.Enter) RestartRequested = true;
                else if (k == Keys.Escape || k == Keys.M) ExitRequested = true;
                return;
            }

            if (paused)
            {
                if (k == Keys.P || k == Keys.Escape || k == Keys.Enter) paused = false;
                else if (k == Keys.M) { Save(); ExitRequested = true; }
                return;
            }

            if (shopOpen)
            {
                if (k == Keys.Up || k == Keys.W) shopSel = (shopSel + 7) % 8;
                else if (k == Keys.Down || k == Keys.S) shopSel = (shopSel + 1) % 8;
                else if (k == Keys.Enter || k == Keys.Space) Buy(shopSel);
                else if (k == Keys.Escape || k == Keys.E) shopOpen = false;
                return;
            }

            if (k == Keys.P || k == Keys.Escape) paused = true;
            else if (k >= Keys.D1 && k <= Keys.D8) slot = k - Keys.D1;
            else if (k == Keys.Space) UseTool();
            else if (k == Keys.E) Interact();
            else if (k == Keys.Z) SleepNow();
        }
    }
}

