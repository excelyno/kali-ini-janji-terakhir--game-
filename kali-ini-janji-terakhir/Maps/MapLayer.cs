using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;


namespace kali_ini_janji_terakhir.Maps
{
    public class MapLayer
    {
        private readonly List<Tile> tiles = new();

        public string Name { get; private set; }

        public MapLayer(string name)
        {
            Name = name;
        }

        public void AddTile(Tile tile)
        {
            tiles.Add(tile);
        }

        public void Render(Graphics graphics)
        {
            foreach (Tile tile in tiles)
            {
                tile.Render(graphics);
            }
        }

    }

}
