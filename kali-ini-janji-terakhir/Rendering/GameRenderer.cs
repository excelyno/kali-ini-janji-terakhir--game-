using kali_ini_janji_terakhir.Maps;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace kali_ini_janji_terakhir.Rendering
{
    public class GameRenderer : IDisposable
    {
        private readonly MapRenderer mapRenderer;
        private readonly MapFactory mapFactory;
        private readonly GameMap gameMap;
        private readonly Bitmap grassTexture;

        public GameRenderer()
        {
            mapRenderer = new MapRenderer();
            mapFactory = new MapFactory();

            grassTexture = CreateGrassTexture();

            gameMap = mapFactory.CreateTestMap(grassTexture);
        }

        public void Render(Graphics graphics)
        {
            graphics.Clear(Color.Black);

            mapRenderer.Render(graphics, gameMap);
        }

        private Bitmap CreateGrassTexture()
        {
            Bitmap bitmap = new Bitmap(32, 32);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.ForestGreen);

                using (SolidBrush brush = new SolidBrush(Color.Green))
                {
                    graphics.FillRectangle(brush, 5, 7, 3, 3);
                    graphics.FillRectangle(brush, 20, 15, 3, 3);
                    graphics.FillRectangle(brush, 12, 25, 3, 3);
                }
            }

            return bitmap;
        }

        public void Dispose()
        {
            grassTexture.Dispose();
        }
    }
}