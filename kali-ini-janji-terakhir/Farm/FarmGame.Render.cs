using System.Drawing;
using System.Linq;
using kali_ini_janji_terakhir.Farm.Engine;
using kali_ini_janji_terakhir.Farm.Ui;

namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        public void Render(Graphics g)
        {
            var r = new Renderer(g);
            view.Draw(r, camera, showCursor: TopWindow == null);

            r.BeginScreen();
            int vw = camera.ViewWidth, vh = camera.ViewHeight;
            if (!windows.OfType<EndingWindow>().Any()) Hud.Draw(r, vw, vh, world, showPrompt: TopWindow == null);
            TopWindow?.Draw(r, vw, vh, world);   // hanya jendela teratas, agar tidak saling tumpuk
        }
    }
}
