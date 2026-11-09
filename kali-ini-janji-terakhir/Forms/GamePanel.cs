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
        public GamePanel()
        {
            InitializePanel();

        }
        private void InitializePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Black;

            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            renderer.Render(e.Graphics);
        }

        private readonly GameRenderer renderer = new GameRenderer();
    }
}
