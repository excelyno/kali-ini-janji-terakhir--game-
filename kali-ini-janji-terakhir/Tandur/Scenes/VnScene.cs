using Tandur.Engine;
using Tandur.Ui;
using Tandur.Vn;

namespace Tandur.Scenes
{
    /// <summary>Menampilkan VnRuntime di layar. Overlay: gameplay tetap terlihat di bawahnya.</summary>
    public sealed class VnScene : Scene
    {
        private const float PortraitScale = 4, Cps = 48;
        private static readonly Dictionary<string, float> SlotX = new() { ["left"] = 0.2f, ["center"] = 0.5f, ["right"] = 0.8f };

        private readonly string scriptName, label;
        private readonly Action? onFinish;
        private VnRuntime rt = null!;
        private float fade, shown, skipTimer, time, pause, cm;
        private bool closing;
        private List<string> lines = new();
        private int lineId = -1, choice;
        private readonly Dictionary<string, (float A, string Slot, string Expr, float Target)> visuals = new();
        private string? bgName;
        private float bgAlpha;
        private List<(float X, float Y, float W, float H)> choiceRects = new();
        private readonly List<Spring> clift = new();
        private (float, float)? lastMouse;

        public VnScene(string script, string label = "start", Action? onFinish = null)
        {
            scriptName = script;
            this.label = label;
            this.onFinish = onFinish;
        }

        public override bool Transparent => true;

        private static float E(float t) { t = Ease.Clamp01(t); return 1 - MathF.Pow(1 - t, 3); }

        public override void OnEnter()
        {
            rt = new VnRuntime(Game.Vn.Load(scriptName), Game.State, Game.Vn.Load, (n, a) => Game.Events.Emit(n, a));
            rt.Start(label);
        }

        private void Finish()
        {
            Game.Scenes.Pop();
            onFinish?.Invoke();
        }

        private (float X, float Y, float W, float H) Box()
        {
            var (w, h) = Game.Gfx.UiSize;
            float bw = Math.Min(w - 80, 1120), bh = 176;
            return ((w - bw) / 2, h - bh - 26, bw, bh);
        }

        public override void Update(float dt)
        {
            var inp = Game.Input;
            time += dt;
            float step = dt / 0.25f;
            fade = closing ? Math.Max(0, fade - step) : Math.Min(1, fade + step);
            if (closing) { if (fade <= 0) Finish(); return; }
            rt.Update(dt);
            if (rt.Finished) { closing = true; return; }
            SyncVisuals(dt);
            if (rt.LineId != lineId)
            {
                lineId = rt.LineId;
                shown = 0; pause = 0;
                lines = Game.Font.Wrap(rt.Text, Theme.Body, Box().W - 88);
            }
            bool confirm = inp.Pressed(InputAction.Confirm) || inp.Clicked;
            if (rt.Mode == VnMode.Say)
            {
                int total = rt.Text.Length;
                if (shown < total)
                {
                    bool fast = inp.Held(InputAction.Skip);
                    if (pause > 0 && !fast) pause -= dt;
                    else
                    {
                        int before = (int)shown;
                        shown = Math.Min(total, shown + Cps * (fast ? 8 : 1) * dt);
                        int after = (int)shown;
                        if (after > before && after <= total && !fast)
                        {
                            char ch = rt.Text[after - 1];
                            pause = ".!?".Contains(ch) ? 0.24f : ",;:".Contains(ch) ? 0.11f : 0;
                        }
                    }
                    if (confirm) shown = total;
                }
                else if (confirm) rt.Advance();
                else if (inp.Held(InputAction.Skip))
                {
                    skipTimer += dt;
                    if (skipTimer > 0.07f) { skipTimer = 0; rt.Advance(); }
                }
            }
            else if (rt.Mode == VnMode.Choice)
            {
                int n = rt.Choices.Count;
                cm += dt;
                while (clift.Count < n) clift.Add(new Spring(0, 320, 20));
                for (int i = 0; i < clift.Count; i++) { clift[i].Target = i == choice ? -5 : 0; clift[i].Update(dt); }
                if (inp.Pressed(InputAction.Up)) choice = (choice - 1 + n) % n;
                if (inp.Pressed(InputAction.Down)) choice = (choice + 1) % n;
                var (mx, my) = Game.MouseUi();
                int? hovered = null;
                for (int i = 0; i < choiceRects.Count; i++)
                {
                    var (x, y, w, h) = choiceRects[i];
                    if (x <= mx && mx <= x + w && y <= my && my <= y + h) hovered = i;
                }
                if (hovered != null && lastMouse != (mx, my) && inp.CursorSeen) choice = hovered.Value;
                lastMouse = (mx, my);
                if (inp.Pressed(InputAction.Confirm) || (inp.Clicked && hovered != null))
                {
                    int idx = Math.Min(choice, n - 1);
                    choice = 0;
                    rt.Choose(idx);
                }
            }
        }

        private void SyncVisuals(float dt)
        {
            foreach (var (id, a) in rt.Actors)
            {
                float alpha = visuals.TryGetValue(id, out var v) ? v.A : 0;
                visuals[id] = (alpha, a.Slot, a.Expr, 1);
            }
            foreach (var id in visuals.Keys.ToList())
            {
                var v = visuals[id];
                if (!rt.Actors.ContainsKey(id)) v.Target = 0;
                v.A += (v.Target - v.A) * Math.Min(1, dt * 10);
                if (v.Target == 0 && v.A < 0.02f) visuals.Remove(id); else visuals[id] = v;
            }
            if (rt.Bg != bgName)
            {
                bgAlpha = Math.Max(0, bgAlpha - dt * 4);
                if (bgAlpha <= 0) bgName = rt.Bg;
            }
            else if (bgName != null) bgAlpha = Math.Min(1, bgAlpha + dt * 3);
        }

        public override void Draw(Renderer gfx)
        {
            if (rt == null) return;
            var batch = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            float f = E(fade);
            if (bgName != null && E(bgAlpha) > 0 && Game.Atlas.Find($"bg_{bgName}") is { } region)
            {
                float s = Math.Max(w / region.W, h / region.H);
                batch.Quad(region, (w - region.W * s) / 2, (h - region.H * s) / 2, region.W * s, region.H * s, new Rgba(1, 1, 1, f * E(bgAlpha)));
                batch.Rect(0, 0, w, h, new Rgba(0, 0, 0, 0.12f * f));
            }
            else batch.Rect(0, 0, w, h, new Rgba(0.02f, 0.03f, 0.07f, 0.42f * f));
            var (bx, by, bw, bh) = Box();
            DrawStage(batch, w, by, f);
            DrawBox(batch, font, bx, by, bw, bh, f);
            if (rt.Mode != VnMode.Choice) cm = 0;
            if (rt.Mode == VnMode.Choice) DrawChoices(batch, font, w, h, f);
            string hint = rt.Mode == VnMode.Say ? "Spasi / E / klik: lanjut     Ctrl: percepat" : "W / S + Enter, atau klik";
            Theme.Text(font, batch, hint, bx + bw - font.Measure(hint, Theme.Small) - 8, by - 30, Theme.Small, Theme.Cream, 0.85f * f);
        }

        private void DrawStage(SpriteBatch batch, float w, float boxTop, float f)
        {
            bool typing = rt.Mode == VnMode.Say && shown < rt.Text.Length;
            foreach (var (id, v) in visuals)
            {
                if (!Game.Vn.Characters.TryGetValue(id, out var def)) continue;
                var region = Game.Atlas.Find($"{def.Portrait}_{v.Expr}") ?? Game.Atlas.Find($"{def.Portrait}_neutral");
                if (region == null) continue;
                float cx = w * SlotX.GetValueOrDefault(v.Slot, 0.5f);
                float slide = (1 - E(v.A)) * (v.Slot == "left" ? -40 : v.Slot == "right" ? 40 : 0);
                bool speaking = rt.Speaker == null || rt.Speaker == id;
                float tone = speaking ? 1 : 0.55f, lift = speaking ? 0 : 10;
                int phase = id.Sum(c => c) % 7;
                float bob = MathF.Round(MathF.Sin(time * 2 + phase)) * 3;
                float talk = speaking && typing && (int)(time * 9) % 2 == 1 ? -3 : 0;
                batch.Sprite(region, cx + slide, boxTop + 46 + lift + bob + talk, new Rgba(tone, tone, Math.Min(1, tone + 0.06f), v.A * f), scale: PortraitScale);
            }
        }

        private void DrawBox(SpriteBatch batch, BitmapFont font, float x, float y, float w, float h, float f)
        {
            y += (1 - E(fade)) * 36;
            Theme.Panel(batch, x, y, w, h, f, "paper");
            if (rt.Speaker != null)
            {
                Game.Vn.Characters.TryGetValue(rt.Speaker, out var def);
                string name = def?.Name ?? rt.Speaker;
                var color = def?.Color ?? Theme.Gold;
                float nw = font.Measure(name, Theme.Ui) + 84;
                Theme.Panel(batch, x + 30, y - 32, nw, 54, f, shadow: false);
                batch.Rect(x + 50, y - 12, 14, 14, Theme.Outline.WithA(f));
                batch.Rect(x + 52, y - 10, 10, 10, color.WithA(f));
                Theme.Text(font, batch, name, x + 76, y - 22, Theme.Ui, Theme.Cream, f);
            }
            int remaining = (int)shown;
            float ty = y + 26, lh = font.LineHeight(Theme.Body) + 2;
            foreach (var line in lines)
            {
                if (remaining <= 0) break;
                Theme.InkText(font, batch, line[..Math.Min(remaining, line.Length)], x + 44, ty, Theme.Body, Theme.Ink, f);
                remaining -= line.Length + 1;
                ty += lh;
            }
            if (rt.Mode == VnMode.Say && shown >= rt.Text.Length)
            {
                var leaf = Game.Atlas["ui_next"];
                float bob = MathF.Sin(time * 5) * 4;
                batch.Quad(leaf, x + w - 70, y + h - 62 + bob, leaf.W * 3, leaf.H * 3, new Rgba(1, 1, 1, f));
            }
        }

        private void DrawChoices(SpriteBatch batch, BitmapFont font, float w, float h, float f)
        {
            float cw = Math.Min(820, w - 120), ch = 60, gap = 12;
            int n = rt.Choices.Count;
            float total = n * ch + (n - 1) * gap, x0 = (w - cw) / 2, y0 = (h - 230 - total) / 2;
            choiceRects = new();
            for (int i = 0; i < n; i++)
            {
                float y = y0 + i * (ch + gap);
                bool sel = i == choice;
                choiceRects.Add((x0, y, cw, ch));
                float enter = Ease.OutBack((cm - i * 0.06f) / 0.28f);
                if (enter <= 0) continue;
                float a = f * Math.Min(1, enter), slide = (1 - Math.Min(1, enter)) * 60;
                float yy = y + (i < clift.Count ? clift[i].Value : 0);
                bool pressed = sel && Game.Input.MouseDown;
                Theme.Button(batch, x0 + slide, yy, cw, ch, pressed ? "down" : sel ? "hover" : "normal", a);
                if (sel)
                {
                    Theme.Frame(batch, x0 + slide, yy, cw, ch, Theme.Gold, a * (0.75f + 0.25f * MathF.Sin(time * 6)));
                    batch.Rect(x0 + slide + 24, yy + ch / 2 - 6, 12, 12, Theme.Outline.WithA(a));
                    batch.Rect(x0 + slide + 26, yy + ch / 2 - 4, 8, 8, Theme.Gold.WithA(a));
                }
                Theme.Text(font, batch, rt.Choices[i].Text, x0 + slide + 52, yy + ch / 2 - 14, Theme.Ui, sel ? Theme.Gold : Theme.Cream, a);
            }
        }
    }
}
