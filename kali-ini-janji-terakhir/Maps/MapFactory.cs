using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace kali_ini_janji_terakhir.Maps
{
    public class MapFactory
    {
        public GameMap CreateTestMap(Bitmap grassTexture)
        {
            const int mapWidth = 20;
            const int mapHeight = 15;
            const int tileSize = 32;

            GameMap map = new GameMap(
                mapWidth,
                mapHeight,
                tileSize
            );

            MapLayer groundLayer = new MapLayer("Ground");

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    Tile tile = new Tile(
                        grassTexture,
                        x,
                        y,
                        tileSize
                    );

                    groundLayer.AddTile(tile);
                }
            }

            map.AddLayer(groundLayer);

            return map;
        }
    }
}