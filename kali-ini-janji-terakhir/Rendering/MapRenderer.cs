using kali_ini_janji_terakhir.Maps;
using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;


namespace kali_ini_janji_terakhir.Rendering
{
    public class MapRenderer
    {
        public void Render(Graphics graphics, GameMap map)
        {
            map.Render(graphics);
        }
    }
}