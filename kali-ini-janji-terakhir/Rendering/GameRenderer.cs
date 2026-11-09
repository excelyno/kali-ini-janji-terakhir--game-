using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace kali_ini_janji_terakhir.Rendering
{
    
    public class GameRenderer
    {

        private void DrawBackground(Graphics graphics)
        {
            // Draw the background here
            graphics.Clear(Color.Black);
        }

        public void Render(Graphics graphics)
        {
            DrawBackground(graphics);
        }
    }

}
