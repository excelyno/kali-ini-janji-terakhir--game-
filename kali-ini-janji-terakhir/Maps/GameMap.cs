using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace kali_ini_janji_terakhir.Maps
{
    public class GameMap
    {
        private readonly List<MapLayer> layers = new();
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileSize { get; private set; }

        public GameMap(int width, int height, int tileSize)
        {
            Width = width;
            Height = height;
            TileSize = tileSize;
        }

        public void AddLayer(MapLayer layer)
        {
            layers.Add(layer);
        }

        public void Render(Graphics graphics)
        {
            foreach (MapLayer layer in layers)
            {
                layer.Render(graphics);
            }
        }

    }
    }
