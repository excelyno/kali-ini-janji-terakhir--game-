using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;


namespace kali_ini_janji_terakhir.Maps
{
    public class Tile
    {
        public Bitmap Texture { get; private set; }

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public int TileSize { get; private set; }

        public Tile(Bitmap texture, int gridX, int gridY, int tileSize)
        {
            Texture = texture;
            GridX = gridX;
            GridY = gridY;
            TileSize = tileSize;
        }

        public Rectangle GetBounds()
        {
            return new Rectangle(GridX * TileSize, GridY * TileSize, TileSize, TileSize);
        }

        public void Draw(Graphics graphics)
        {
            graphics.DrawImage(Texture, GetBounds());
        }
    }
}
