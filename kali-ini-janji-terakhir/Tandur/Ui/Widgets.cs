using Tandur.Engine;

namespace Tandur.Ui
{
    /// <summary>Tumpukan tombol kayu vertikal. Mouse (hover/klik) dan W/S/Enter sama-sama bisa.</summary>
    public sealed class ButtonStack
    {
        private readonly List<Spring> lift, pop;
        private (float, float)? lastMouse;
        private float t;

        public ButtonStack(IEnumerable<string> labels, float width = 340, float height = 62, float gap = 14)
        {
            Labels = labels.ToList();
            W = width; H = height; Gap = gap;
            lift = Labels.Select(_ => new Spring(0, 320, 20)).ToList();
            pop = Labels.Select(_ => new Spring(0, 220, 16)).ToList();
        }

        public List<string> Labels { get; }
        public float W { get; }
        public float H { get; }
        public float Gap { get; }
        public int Sel { get; set; }
        public List<(float X, float Y, float W, float H)> Rects { get; private set; } = new();
        public float TotalHeight => Labels.Count * H + (Labels.Count - 1) * Gap;

        public void Place(float cx, float top)
            => Rects = Enumerable.Range(0, Labels.Count).Select(i => (cx - W / 2, top + i * (H + Gap), W, H)).ToList();

        public void Intro(float delay = 0.07f)
        {
            for (int i = 0; i < pop.Count; i++) pop[i].Snap(-1 - i * delay * 8);
        }

        public int? Update(TandurGame game, float dt)
        {
            var inp = game.Input;
            t += dt;
            int n = Labels.Count;
            if (inp.Pressed(InputAction.Up)) Sel = (Sel - 1 + n) % n;
            if (inp.Pressed(InputAction.Down)) Sel = (Sel + 1) % n;
            var (mx, my) = game.MouseUi();
            int? hovered = null;
            for (int i = 0; i < Rects.Count; i++)
            {
                var (x, y, w, h) = Rects[i];
                if (x <= mx && mx <= x + w && y <= my && my <= y + h) hovered = i;
            }
            if (hovered != null && lastMouse != (mx, my) && inp.CursorSeen) Sel = hovered.Value;
            lastMouse = (mx, my);
            for (int i = 0; i < n; i++)
            {
                lift[i].Target = i == Sel ? -5 : 0;
                pop[i].Target = 0;
                lift[i].Update(dt);
                pop[i].Update(dt);
            }
            if (inp.Clicked && hovered != null) { Sel = hovered.Value; lift[Sel].Kick(60); return Sel; }
            if (inp.Pressed(InputAction.Confirm)) { lift[Sel].Kick(60); return Sel; }
            return null;
        }

        public void Draw(TandurGame game, SpriteBatch batch, BitmapFont font, float alpha = 1)
        {
            var (mx, my) = game.MouseUi();
            for (int i = 0; i < Rects.Count; i++)
            {
                var (x, y, w, h) = Rects[i];
                float slide = Math.Max(0, -pop[i].Value) * 40;
                float a = alpha * Math.Clamp(1 + pop[i].Value * 0.9f, 0, 1);
                float yy = y + lift[i].Value;
                bool sel = i == Sel;
                bool hot = sel && game.Input.MouseDown && x <= mx && mx <= x + w && y <= my && my <= y + h;
                Theme.Button(batch, x - slide, yy, w, h, hot ? "down" : sel ? "hover" : "normal", a);
                if (sel) Theme.Frame(batch, x - slide, yy, w, h, Theme.Gold, a * (0.75f + 0.25f * MathF.Sin(t * 6)));
                Theme.TextCentered(font, batch, Labels[i], x + w / 2 - slide, yy + h / 2 - 15 + (hot ? 3 : 0), Theme.Ui, sel ? Theme.Gold : Theme.Cream, a);
            }
        }
    }

    /// <summary>Notifikasi bertumpuk di kiri bawah: papan kayu dengan ikon yang memantul masuk dari tepi.</summary>
    public sealed class ToastQueue
    {
        private sealed class Toast { public string Text = ""; public string? Icon; public float Age; public Spring Slide = null!; public Spring Bump = null!; }

        private readonly float ttl;
        private readonly int maxVisible;
        private readonly List<Toast> items = new();

        public ToastQueue(float ttl = 3.4f, int maxVisible = 4) { this.ttl = ttl; this.maxVisible = maxVisible; }

        public void Add(string text, string? icon = null)
        {
            if (items.Count > 0 && items[^1].Text == text && items[^1].Age < 1.5f)
            {
                items[^1].Age = 0.5f;
                items[^1].Bump.Kick(120);
                return;
            }
            var slide = new Spring(-1, 260, 17) { Target = 0 };
            items.Add(new Toast { Text = text, Icon = icon, Slide = slide, Bump = new Spring(0, 400, 14) });
            while (items.Count > maxVisible) items.RemoveAt(0);
        }

        public void Update(float dt)
        {
            foreach (var i in items) { i.Age += dt; i.Slide.Update(dt); i.Bump.Update(dt); }
            items.RemoveAll(i => i.Age >= ttl);
        }

        public void Draw(SpriteBatch batch, BitmapFont font, Atlas atlas, float uiH, float? baseY = null)
        {
            float y = baseY ?? uiH - 150;
            for (int k = items.Count - 1; k >= 0; k--)
            {
                var it = items[k];
                float fade = Ease.Clamp01(Math.Min(it.Age / 0.12f, (ttl - it.Age) / 0.45f));
                var icon = it.Icon != null ? atlas.Find(it.Icon) : null;
                float tw = font.Measure(it.Text, Theme.Ui);
                float w = tw + 44 + (icon != null ? icon.W * 2 + 10 : 0), h = 56;
                float x = 18 + it.Slide.Value * (w + 30);
                float yy = y - h - it.Bump.Value * 0.15f;
                Theme.Panel(batch, x, yy, w, h, fade, shadow: false);
                float tx = x + 22;
                if (icon != null)
                {
                    batch.Quad(icon, x + 20, yy + 12, icon.W * 2, icon.H * 2, new Rgba(1, 1, 1, fade));
                    tx += icon.W * 2 + 10;
                }
                Theme.Text(font, batch, it.Text, tx, yy + 15, Theme.Ui, Theme.Cream, fade);
                y -= h + 8;
            }
        }
    }
}
