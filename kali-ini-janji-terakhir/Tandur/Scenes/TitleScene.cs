using Tandur.Core;
using Tandur.Engine;
using Tandur.Ui;

namespace Tandur.Scenes
{
    /// <summary>Layar judul: papan kayu tergantung di atas lukisan fajar, kunang-kunang melayang.</summary>
    public sealed class TitleScene : Scene
    {
        private float t, fadeOut;
        private bool starting, confirmNew;
        private (int Day, string Season, int Gold, int Debt)? info;
        private ButtonStack menu = null!;
        private readonly List<(float X, float Y, float Sp, float Ph, float Size)> motes = new();

        public override void OnEnter()
        {
            info = Game.Saves.Info();
            menu = new ButtonStack(info != null ? new[] { "Lanjutkan", "Mulai Baru", "Keluar" } : new[] { "Mulai Bertani", "Keluar" }, 360, 64, 14);
            menu.Intro();
            var rng = new Random(5);
            for (int i = 0; i < 34; i++)
                motes.Add(((float)rng.NextDouble(), (float)rng.NextDouble(), 0.02f + (float)rng.NextDouble() * 0.04f, (float)rng.NextDouble() * 6.28f, new[] { 3f, 3f, 6f }[rng.Next(3)]));
        }

        public override void Update(float dt)
        {
            var g = Game;
            t += dt;
            if (starting)
            {
                fadeOut += dt / 0.6f;
                if (fadeOut >= 1) g.Scenes.Replace(new PlayScene());
                return;
            }
            var (w, h) = g.Gfx.UiSize;
            menu.Place(w / 2, h * 0.58f);
            if (t < 1.1f) return;
            int? choice = menu.Update(g, dt);
            if (g.Input.Pressed(InputAction.Cancel)) g.Quit();
            if (choice == null) return;
            if (info != null)
            {
                if (choice == 0) { if (!g.LoadGame()) g.NewGame(); starting = true; }
                else if (choice == 1)
                {
                    if (!confirmNew) { confirmNew = true; menu.Labels[1] = "Yakin? Save lama diganti"; }
                    else { g.NewGame(); starting = true; }
                }
                else g.Quit();
            }
            else if (choice == 0) { g.NewGame(); starting = true; }
            else g.Quit();
        }

        public override void Draw(Renderer gfx)
        {
            var batch = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            var bg = Game.Atlas["bg_dawn"];
            float s = Math.Max((w + 20) / bg.W, (h + 4) / bg.H), fadeIn = Math.Min(1, t / 0.8f);
            // parallax halus: latar sedikit bergeser mengikuti waktu (versi C#)
            float drift = MathF.Sin(t * 0.15f) * 8;
            batch.Quad(bg, (w - bg.W * s) / 2 + drift, (h - bg.H * s) / 2, bg.W * s, bg.H * s, new Rgba(1, 1, 1, fadeIn));
            batch.Rect(0, 0, w, h, new Rgba(0, 0, 0, 0.12f));
            foreach (var (mx, my, sp, ph, size) in motes)
            {
                float x = (mx + MathF.Sin(t * 0.3f + ph) * 0.03f) * w;
                float y = ((my - t * sp) % 1f + 1f) % 1f * h;
                float a = (0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(t * 2.2f + ph))) * fadeIn;
                batch.Rect(MathF.Round(x), MathF.Round(y), size, size, new Rgba(1f, 0.92f, 0.55f, a * 0.9f));
            }
            float drop = Ease.OutBack((t - 0.2f) / 0.9f);
            float sway = MathF.Round(MathF.Sin(t * 1.3f) * 1.2f) * 3;
            float pw = 640, ph2 = 262, px = (w - pw) / 2, py = 54 - (1 - drop) * 340 + sway;
            foreach (float rx in new[] { px + 70, px + pw - 70 })
            {
                batch.Rect(rx - 4, 0, 8, py + 12, Theme.Outline);
                batch.Rect(rx - 2, 0, 4, py + 12, new Rgba(0.72f, 0.58f, 0.36f));
            }
            Theme.Panel(batch, px, py, pw, ph2);
            float al = Math.Clamp((t - 0.7f) / 0.6f, 0, 1), cx = w / 2;
            Theme.TextCentered(font, batch, "Musim pertama menantimu", cx, py + 32, Theme.Ui, Theme.Gold, al);
            Theme.TextCentered(font, batch, "TANDUR", cx, py + 66, Theme.Title, Theme.Cream, al);
            batch.Rect(cx - 150 * al, py + 140, 300 * al, 3, Theme.Gold.WithA(al));
            Theme.TextCentered(font, batch, "Where the Seasons Begin", cx, py + 154, Theme.Body, Theme.Cream, al);
            Theme.TextCentered(font, batch, "Cangkul, tanam, siram, panen.", cx, py + 204, Theme.Ui, Theme.Leaf, al);
            if (t >= 0.9f)
            {
                menu.Place(cx, h * 0.58f);
                menu.Draw(Game, batch, font);
            }
            if (info is { } i && t >= 0.9f)
                Theme.TextCentered(font, batch, $"Hari {i.Day}, Musim {i.Season}  |  {i.Gold} keping  |  utang {i.Debt}", cx, h - 74, Theme.Small + 4, Theme.Gold, 0.9f * al);
            Theme.Text(font, batch, "W / S + Enter, atau klik", 22, h - 34, Theme.Small, Theme.Cream, 0.75f * al);
            if (starting)
            {
                float k = Ease.OutCubic(fadeOut);
                batch.Rect(0, 0, w, h / 2 * k, new Rgba(0.04f, 0.03f, 0.02f));
                batch.Rect(0, h - h / 2 * k, w, h / 2 * k + 1, new Rgba(0.04f, 0.03f, 0.02f));
            }
        }
    }
}
