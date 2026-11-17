using Tandur.Core;
using Tandur.Engine;
using Tandur.Minigames;
using Tandur.Ui;

namespace Tandur.Scenes
{
    /// <summary>Overlay minigame panen + kartu hasil. Semua aturan ada di Minigames; di sini hanya input dan gambar.</summary>
    public sealed class MinigameScene : OverlayScene
    {
        private const float Pw = 820, Ph = 500, StartDelay = 0.55f;
        private static readonly Rgba Soil = new(0.36f, 0.24f, 0.14f), SoilHi = new(0.47f, 0.32f, 0.19f);

        private readonly PlayScene play;
        private readonly string crop;
        private readonly bool block;
        private readonly Action<MiniResult> onDone;
        private MiniGame gl = null!;
        private bool resultPhase;
        private float rt, mouseT = -9, shake;
        private List<Fact> newFacts = new();
        private readonly List<Spring> stars = Enumerable.Range(0, 3).Select(_ => new Spring(0, 320, 12)).ToList();
        private int sel;
        private (float, float) lastCursor;
        private (string Text, Rgba Color, float Age)? flash;
        private readonly List<(float X, float Y, string Text, Rgba Color, float Age)> fx = new();

        public MinigameScene(PlayScene play, string crop, bool block, Action<MiniResult> onDone)
        {
            this.play = play; this.crop = crop; this.block = block; this.onDone = onDone;
        }

        public override void OnEnter()
        {
            gl = MiniGame.Create(crop, block, HashCode.Combine(crop, play.Time) & 0xFFFF);
            lastCursor = Game.Input.Cursor;
        }

        private void CloseWith(bool deliver)
        {
            if (Closing) return;
            Close();
            if (deliver) onDone(gl.Result!);
        }

        public override void Update(float dt)
        {
            var inp = Game.Input;
            Tick(dt);
            if (HandleClosing()) return;
            if (inp.Pressed(InputAction.Cancel)) { CloseWith(false); return; }
            if (flash is { } f) flash = f.Age + dt > 0.9f ? null : (f.Text, f.Color, f.Age + dt);
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var e = fx[i];
                if (e.Age + dt >= 0.9f) fx.RemoveAt(i); else fx[i] = e with { Age = e.Age + dt };
            }
            if (!resultPhase)
            {
                if (T < StartDelay) return;
                Drive(dt);
                if (gl.Done)
                {
                    resultPhase = true;
                    rt = 0;
                    newFacts = play.Journal.Unlock(gl.Result!.Facts);
                }
            }
            else
            {
                rt += dt;
                for (int i = 0; i < 3; i++)
                {
                    stars[i].Target = i < gl.Result!.Stars && rt > 0.25f + 0.28f * i ? 1 : 0;
                    stars[i].Update(dt);
                }
                if (rt > 0.9f && (inp.Pressed(InputAction.Confirm) || inp.Pressed(InputAction.Use) || inp.Clicked)) CloseWith(true);
            }
        }

        private void Drive(float dt)
        {
            var inp = Game.Input;
            var (mx, my) = Game.MouseUi();
            if (inp.Cursor != lastCursor) { lastCursor = inp.Cursor; mouseT = T; }
            bool press = inp.Pressed(InputAction.Use) || inp.Clicked;
            switch (gl)
            {
                case TurnipPull tp:
                {
                    float steer = (inp.Held(InputAction.Right) ? 1 : 0) - (inp.Held(InputAction.Left) ? 1 : 0);
                    if (steer == 0 && inp.CursorSeen && T - mouseT < 1) steer = Math.Clamp((mx - Game.Gfx.UiSize.W / 2) / 180, -1, 1);
                    int was = tp.Snaps;
                    tp.Update(dt, new MiniInput { Pull = inp.Held(InputAction.Use) || inp.MouseDown, Steer = steer });
                    if (tp.Snaps > was) { shake = 0.35f; flash = ("Krek! Akar tegang", Theme.Berry, 0); }
                    break;
                }
                case CarrotRhythm cr:
                {
                    int before = cr.Results.Count;
                    if (press) cr.Press();
                    cr.Update(dt, new MiniInput());
                    if (cr.Results.Count > before)
                    {
                        string k = cr.Results[^1];
                        flash = (k == "perfect" ? "Pas!" : k == "good" ? "Bagus" : "Meleset", k == "perfect" ? Theme.Gold : k == "good" ? Theme.Leaf : Theme.Berry, 0);
                    }
                    break;
                }
                case TomatoPick tm:
                {
                    int cols = TomatoCols, n = tm.Kinds.Count;
                    if (inp.Pressed(InputAction.Left)) sel = (sel - 1 + n) % n;
                    if (inp.Pressed(InputAction.Right)) sel = (sel + 1) % n;
                    if (inp.Pressed(InputAction.Up)) sel = ((sel - cols) % n + n) % n;
                    if (inp.Pressed(InputAction.Down)) sel = (sel + cols) % n;
                    int? hover = TomatoAt(mx, my);
                    if (hover != null && T - mouseT < 0.5f) sel = hover.Value;
                    int? target = inp.Clicked && hover != null ? hover : inp.Pressed(InputAction.Use) || inp.Pressed(InputAction.Interact) ? sel : null;
                    if (target is int ti)
                    {
                        var before = tm.Last;
                        tm.Pick(ti);
                        if (tm.Last != before && tm.Last is { } last)
                        {
                            var (x, y) = TomatoPos(last.Index);
                            fx.Add((x, y, last.Kind == "good" ? "+1" : "Belum matang!", last.Kind == "good" ? Theme.Leaf : Theme.Berry, 0));
                        }
                    }
                    tm.Update(dt, new MiniInput());
                    break;
                }
            }
            shake = Math.Max(0, shake - dt);
        }

        private (float X, float Y) Panel()
        {
            var (w, h) = Game.Gfx.UiSize;
            return ((w - Pw) / 2, (h - Ph) / 2 + Drop.Value * 420);
        }

        private int TomatoCols => gl is TomatoPick tm && tm.Kinds.Count > 8 ? 5 : 4;

        private (float X, float Y) TomatoPos(int i)
        {
            var (px, py) = Panel();
            int cols = TomatoCols;
            float cw = (Pw - 120) / cols;
            return (px + 60 + (i % cols + 0.5f) * cw, py + 190 + i / cols * 130);
        }

        private int? TomatoAt(float mx, float my)
        {
            if (gl is not TomatoPick tm) return null;
            for (int i = 0; i < tm.Kinds.Count; i++)
            {
                var (x, y) = TomatoPos(i);
                if (MathF.Abs(mx - x) <= 44 && MathF.Abs(my - y) <= 48) return i;
            }
            return null;
        }

        public override void Draw(Renderer gfx)
        {
            var b = gfx.Batch;
            var font = Game.Font;
            var atlas = Game.Atlas;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            b.Rect(0, 0, w, h, new Rgba(0.03f, 0.02f, 0.05f, DimAlpha(0.55f)));
            var (px, py) = Panel();
            Theme.Panel(b, px, py, Pw, Ph, kind: "paper");
            Theme.InkText(font, b, gl.Title + (block ? "  (blok 2x2)" : ""), px + 48, py + 34, Theme.Body);
            if (!resultPhase)
            {
                var hint = font.Wrap(gl.Hint, Theme.Small, Pw - 110);
                for (int i = 0; i < hint.Count; i++) Theme.InkText(font, b, hint[i], px + 48, py + 78 + i * 20, Theme.Small, Theme.InkDim);
                b.Rect(px + 48, py + 122, Pw - 96, 3, Theme.InkDim.WithA(0.45f));
                switch (gl)
                {
                    case TurnipPull tp: DrawTurnip(b, font, atlas, px, py, tp); break;
                    case CarrotRhythm cr: DrawCarrot(b, font, atlas, px, py, cr); break;
                    case TomatoPick tm: DrawTomato(b, font, atlas, px, py, tm); break;
                }
                if (T < StartDelay) Theme.TextCentered(font, b, "Siap...", px + Pw / 2, py + 250, Theme.Title, Theme.Cream, 1 - T / StartDelay);
                if (flash is { } f) Theme.TextCentered(font, b, f.Text, px + Pw / 2, py + 130 - Ease.OutBack(f.Age / 0.2f) * 6, Theme.Ui, f.Color, Ease.Clamp01((0.9f - f.Age) / 0.3f));
            }
            else DrawResult(b, font, atlas, px, py);
            Theme.InkText(font, b, "Esc: batal", px + Pw - 48 - font.Measure("Esc: batal", Theme.Small), py + Ph - 40, Theme.Small, Theme.InkDim);
        }

        private void CropScene(SpriteBatch b, Atlas atlas, float px, float py, float rise, float sh = 0)
        {
            float cx = px + 190, ground = py + 360;
            b.Sprite(atlas[$"crop_{crop}_3"], cx + sh, ground + 6 - rise, scale: 6);
            b.Rect(px + 70, ground, 240, 70, Soil);
            b.Rect(px + 70, ground, 240, 6, SoilHi);
            for (int k = 0; k < 14; k++) b.Rect(px + 84 + k * 53 % 214, ground + 14 + k * 31 % 46, 6, 6, new Rgba(0.28f, 0.18f, 0.1f));
        }

        private static void Outline(SpriteBatch b, float x, float y, float w, float h) => b.Rect(x - 4, y - 4, w + 8, h + 8, Theme.Outline);

        private void DrawTurnip(SpriteBatch b, BitmapFont font, Atlas atlas, float px, float py, TurnipPull g)
        {
            float sh = MathF.Sin(T * 70) * (g.Pulling ? 3 : 0) * (1 + shake * 3);
            CropScene(b, atlas, px, py, g.Progress * 78, sh);
            float gx = px + 340, gw = 440, gy = py + 190, gh = 46;
            Outline(b, gx, gy, gw, gh);
            b.Rect(gx, gy, gw, gh, new Rgba(0.85f, 0.35f, 0.28f));
            float yel = 0.82f;
            b.Rect(gx + gw * (1 - yel) / 2, gy, gw * yel, gh, new Rgba(0.95f, 0.78f, 0.30f));
            float zw = gw * g.Zone;
            b.Rect(gx + gw / 2 - zw / 2, gy, zw, gh, new Rgba(0.45f, 0.78f, 0.36f));
            float nx = gx + (g.Needle + 1) / 2 * gw;
            b.Rect(nx - 6, gy - 12, 12, gh + 24, Theme.Outline);
            b.Rect(nx - 3, gy - 9, 6, gh + 18, new Rgba(1f, 0.97f, 0.88f));
            Theme.InkText(font, b, "Tegangan akar", gx, gy - 34, Theme.Small + 8);
            float ty = py + 300;
            Theme.InkText(font, b, "Terangkat", gx, ty - 30, Theme.Small + 8);
            Theme.Nine(b, "ui_barframe", gx, ty, gw, 48);
            float fw = (gw - 24) * Ease.Clamp01(g.Progress);
            b.Rect(gx + 12, ty + 12, fw, 24, new Rgba(0.50f, 0.80f, 0.38f));
            b.Rect(gx + 12, ty + 12, fw, 6, new Rgba(0.72f, 0.92f, 0.55f));
            Theme.InkText(font, b, g.Pulling ? "TARIK!" : "tahan Spasi / klik", gx, ty + 62, Theme.Ui, g.Pulling ? Theme.Leaf : Theme.InkDim);
        }

        private void DrawCarrot(SpriteBatch b, BitmapFont font, Atlas atlas, float px, float py, CarrotRhythm g)
        {
            CropScene(b, atlas, px, py, g.Results.Count(r => r != "miss") * 26);
            float gx = px + 340, gw = 440, gy = py + 200, gh = 50;
            Outline(b, gx, gy, gw, gh);
            b.Rect(gx, gy, gw, gh, new Rgba(0.78f, 0.45f, 0.30f));
            b.Rect(gx + gw * (0.5f - g.Good), gy, gw * g.Good * 2, gh, new Rgba(0.95f, 0.78f, 0.30f));
            b.Rect(gx + gw * (0.5f - g.Perfect), gy, gw * g.Perfect * 2, gh, new Rgba(0.45f, 0.78f, 0.36f));
            float mx = gx + g.Marker * gw;
            b.Rect(mx - 7, gy - 12, 14, gh + 24, Theme.Outline);
            b.Rect(mx - 4, gy - 9, 8, gh + 18, new Rgba(1f, 0.97f, 0.88f));
            Theme.InkText(font, b, $"Tarikan {Math.Min(g.Round + 1, 3)} / 3", gx, gy - 40, Theme.Small + 8);
            for (int i = 0; i < 3; i++)
            {
                float x = gx + i * 80;
                string? res = i < g.Results.Count ? g.Results[i] : null;
                b.Rect(x, py + 300, 64, 44, Theme.Outline);
                var c = res switch { "perfect" => Theme.Gold, "good" => Theme.Leaf, "miss" => Theme.Berry, _ => new Rgba(0.82f, 0.72f, 0.52f) };
                b.Rect(x + 4, py + 304, 56, 36, c);
                if (res != null) Theme.TextCentered(font, b, res == "perfect" ? "Pas" : res == "good" ? "Ok" : "X", x + 32, py + 308, Theme.Small + 4, Theme.Cream);
            }
        }

        private void DrawTomato(SpriteBatch b, BitmapFont font, Atlas atlas, float px, float py, TomatoPick g)
        {
            float left = Math.Max(0, g.TimeLimit - g.T), tx = px + 400, tw = 260, ty = py + Ph - 62;
            Theme.InkText(font, b, "Waktu", tx - 70, ty + 8, Theme.Small + 4, Theme.InkDim);
            Theme.Nine(b, "ui_barframe", tx, ty, tw, 36);
            b.Rect(tx + 12, ty + 12, (tw - 24) * Ease.Clamp01(left / g.TimeLimit), 12, left > 5 ? Theme.Leaf : Theme.Berry);
            int cols = TomatoCols;
            for (int r = 0; r < (g.Kinds.Count + cols - 1) / cols; r++)
                b.Rect(px + 56, py + 190 + r * 130 - 58, Pw - 112, 8, new Rgba(0.30f, 0.52f, 0.24f));
            for (int i = 0; i < g.Kinds.Count; i++)
            {
                if (g.Picked[i]) continue;
                var (x, y) = TomatoPos(i);
                float sway = MathF.Sin(T * 2 + i) * 3;
                b.Rect(x - 2, y - 66, 4, 22, new Rgba(0.30f, 0.52f, 0.24f));
                b.Quad(atlas[$"mini_tomato_{g.ColorOf(i)}"], x - 35 + sway, y - 40, 70, 70);
                if (i == sel) Theme.Frame(b, x - 44, y - 50, 88, 96, Theme.Gold, 0.6f + 0.4f * MathF.Sin(T * 6), 4);
            }
            foreach (var (x, y, text, color, age) in fx) Theme.TextCentered(font, b, text, x, y - 30 - age * 40, Theme.Ui, color, Ease.Clamp01((0.9f - age) / 0.3f));
            Theme.InkText(font, b, $"Matang dipetik: {g.GoodCount} / {g.Targets}", px + 48, py + Ph - 40, Theme.Small + 4);
        }

        private void DrawResult(SpriteBatch b, BitmapFont font, Atlas atlas, float px, float py)
        {
            var res = gl.Result!;
            float cx = px + Pw / 2;
            Tandur.Engine.Region star = atlas["ui_star"], off = atlas["ui_star_off"];
            for (int i = 0; i < 3; i++)
            {
                float sx = cx + (i - 1) * 84, v = i < res.Stars ? stars[i].Value : 0;
                b.Quad(off, sx - 26, py + 120 - 26 + 4, 52, 52, new Rgba(1, 1, 1, 0.9f));
                if (i < res.Stars && v > 0.02f)
                {
                    float s = 52 * Math.Max(0, v) * 1.5f;
                    b.Quad(star, sx - s / 2, py + 120 - s / 2 + 4, s, s);
                }
            }
            float y = py + 180;
            foreach (var line in font.Wrap(res.Message, Theme.Ui, Pw - 140))
            {
                Theme.InkText(font, b, line, cx - font.Measure(line, Theme.Ui) / 2, y, Theme.Ui);
                y += 30;
            }
            if (res.Bonus) { Theme.TextCentered(font, b, "Bonus panen blok: +1 hasil!", cx, y + 8, Theme.Ui, Theme.Leaf); y += 38; }
            if (newFacts.Count > 0)
            {
                var fact = newFacts[0];
                string more = newFacts.Count > 1 ? $"  (+{newFacts.Count - 1} catatan lain)" : "";
                var body = font.Wrap(fact.Text, Theme.Small + 4, Pw - 200);
                float ch = 76 + body.Count * 24, fy = Math.Max(y + 18, py + Ph - ch - 70);
                Theme.Panel(b, px + 60, fy, Pw - 120, ch, shadow: false);
                Theme.Text(font, b, $"Catatan Tani baru: {fact.Title}{more}", px + 90, fy + 20, Theme.Ui, Theme.Gold);
                for (int i = 0; i < body.Count; i++) Theme.Text(font, b, body[i], px + 90, fy + 54 + i * 24, Theme.Small + 4, Theme.Cream, outline: false);
            }
            float a = Ease.Clamp01((rt - 0.9f) / 0.3f);
            const string next = "Spasi / klik: lanjut";
            Theme.InkText(font, b, next, cx - font.Measure(next, Theme.Small + 4) / 2, py + Ph - 44, Theme.Small + 4, Theme.Ink, a * (0.6f + 0.4f * MathF.Sin(rt * 5)));
        }
    }

    /// <summary>Catatan Tani: buku fakta bertani yang terbuka lewat minigame.</summary>
    public sealed class JournalScene : OverlayScene
    {
        private const float Pw = 900, Ph = 520, Row = 48;
        private readonly PlayScene play;
        private List<Fact> facts = new();
        private int sel;
        private (float, float)? lastMouse;
        private ButtonStack closeBtn = null!;

        public JournalScene(PlayScene play) => this.play = play;

        public override void OnEnter()
        {
            facts = play.Journal.Facts.Values.ToList();
            closeBtn = new ButtonStack(new[] { "Tutup" }, 180, 52);
        }

        private (float X, float Y) Origin()
        {
            var (w, h) = Game.Gfx.UiSize;
            return ((w - Pw) / 2, (h - Ph) / 2 + Drop.Value * 420);
        }

        private (float X, float Y, float W, float H) RowRect(int i)
        {
            var (px, py) = Origin();
            return (px + 44, py + 96 + i * (Row + 6), 310, Row);
        }

        public override void Update(float dt)
        {
            var inp = Game.Input;
            Tick(dt);
            if (HandleClosing()) return;
            var (px, py) = Origin();
            closeBtn.Place(px + Pw - 44 - 90, py + Ph - 30 - 52);
            if (inp.Pressed(InputAction.Cancel) || inp.Pressed(InputAction.Journal) || closeBtn.Update(Game, dt) != null) { Close(); return; }
            int n = facts.Count;
            if (n == 0) return;
            if (inp.Pressed(InputAction.Up)) sel = (sel - 1 + n) % n;
            if (inp.Pressed(InputAction.Down)) sel = (sel + 1) % n;
            var m = Game.MouseUi();
            if (m != lastMouse && inp.CursorSeen)
            {
                lastMouse = m;
                for (int i = 0; i < n; i++)
                {
                    var (x, y, w, h) = RowRect(i);
                    if (x <= m.X && m.X <= x + w && y <= m.Y && m.Y <= y + h) sel = i;
                }
            }
        }

        public override void Draw(Renderer gfx)
        {
            var b = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            b.Rect(0, 0, w, h, new Rgba(0.03f, 0.02f, 0.05f, DimAlpha(0.55f)));
            var (px, py) = Origin();
            Theme.Panel(b, px, py, Pw, Ph, kind: "paper");
            Theme.InkText(font, b, "Catatan Tani", px + 48, py + 34, Theme.Body);
            var unlocked = play.Journal.UnlockedIds().ToHashSet();
            string count = $"{unlocked.Count} / {facts.Count} terbuka";
            Theme.InkText(font, b, count, px + Pw - 48 - font.Measure(count, Theme.Ui), py + 40, Theme.Ui, Theme.InkDim);
            b.Rect(px + 44, py + 84, Pw - 88, 3, Theme.InkDim.WithA(0.5f));
            for (int i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                var (x, y, rw, rh) = RowRect(i);
                bool open = unlocked.Contains(fact.Id), isSel = i == sel;
                Theme.Button(b, x, y, rw, rh, isSel ? "hover" : "normal", open ? 1 : 0.55f);
                if (isSel) Theme.Frame(b, x, y, rw, rh, Theme.Gold, 0.8f + 0.2f * MathF.Sin(T * 6), 3);
                bool icon = fact.Crop != null && open;
                if (icon) b.Quad(Game.Atlas[$"icon_{fact.Crop}"], x + 10, y + 8, 32, 32);
                Theme.Text(font, b, open ? fact.Title : "???", x + (icon ? 52 : 20), y + 11, Theme.Ui, isSel ? Theme.Gold : Theme.Cream, open ? 1 : 0.6f);
            }
            if (facts.Count > 0)
            {
                var f = facts[sel];
                float tx = px + 400, ty = py + 104;
                bool open = unlocked.Contains(f.Id);
                Theme.InkText(font, b, open ? f.Title : "Catatan terkunci", tx, ty, Theme.Body, open ? Theme.Ink : Theme.InkDim);
                float y = ty + 54;
                foreach (var line in font.Wrap(open ? f.Text : "Mainkan minigame panen dengan baik untuk membuka catatan ini.", Theme.Ui, Pw - 450))
                {
                    Theme.InkText(font, b, line, tx, y, Theme.Ui, open ? Theme.Ink : Theme.InkDim);
                    y += 32;
                }
            }
            closeBtn.Draw(Game, b, font);
        }
    }
}
