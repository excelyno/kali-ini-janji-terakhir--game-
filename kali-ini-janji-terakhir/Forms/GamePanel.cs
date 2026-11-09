using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using kali_ini_janji_terakhir.Rendering;

namespace kali_ini_janji_terakhir.Forms
{
    public class GamePanel : Panel
    {
        private readonly GameRenderer renderer;

        public GamePanel()
        {
            renderer = new GameRenderer();

            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            renderer.Render(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}