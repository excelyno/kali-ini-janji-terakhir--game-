using Tandur.Core;
using Tandur.Engine;
using Tandur.Entities;
using Tandur.Ui;

namespace Tandur.Scenes
{
    /// <summary>
    /// ABSTRAKSI + PEWARISAN: semua panel overlay (jeda, tas, misi, catatan) jatuh masuk dengan pegas
    /// dan menutup dengan cara yang sama. Subclass hanya menulis isinya.
    /// </summary>
    public abstract class OverlayScene : Scene
    {
        protected float T;
        protected readonly Spring Drop = new(-1, 240, 17) { Target = 0 };
        protected bool Closing;

        public override bool Transparent => true;

        protected void Tick(float dt)
        {
            T += dt;
            Drop.Target = Closing ? -1.6f : 0;
            Drop.Update(dt);
        }

        protected void Close() { Closing = true; T = 0; }

        /// <summary>true jika animasi tutup selesai dan scene sudah di-pop.</summary>
        protected bool HandleClosing()
        {
            if (!Closing) return false;
            if (T > 0.28f) Game.Scenes.Pop();
            return true;
        }

        protected float DimAlpha(float k = 0.5f) => k * (Closing ? Math.Max(0, 1 - T / 0.25f) : Math.Min(1, T / 0.2f));
    }

    public sealed class PauseScene : OverlayScene
    {
        private const float Ph = 350;
        private readonly PlayScene play;
        private ButtonStack menu = null!;

        public PauseScene(PlayScene play) => this.play = play;

        private string ModeLabel => Game.State.GetString("mini_mode", "skip") == "always" ? "Minigame: selalu main" : "Minigame: lewati jika ahli";

        public override void OnEnter()
        {
            menu = new ButtonStack(new[] { "Lanjutkan", ModeLabel, "Simpan & ke judul" }, 400, 54, 10);
            menu.Intro(0.05f);
        }

        public override void Update(float dt)
        {
            Tick(dt);
            var (w, h) = Game.Gfx.UiSize;
            menu.Place(w / 2, h * 0.5f - Ph / 2 + 100 + Drop.Value * 420);
            if (HandleClosing()) return;
            if (Game.Input.Pressed(InputAction.Cancel) || Game.Input.Pressed(InputAction.Inventory)) { Close(); return; }
            switch (menu.Update(Game, dt))
            {
                case 0: Close(); break;
                case 1:
                    Game.State.Set("mini_mode", Game.State.GetString("mini_mode", "skip") == "skip" ? "always" : "skip");
                    menu.Labels[1] = ModeLabel;
                    break;
                case 2:
                    play.SaveGame(true);
                    Game.Scenes.Replace(new TitleScene());
                    break;
            }
        }

        public override void Draw(Renderer gfx)
        {
            var b = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            b.Rect(0, 0, w, h, new Rgba(0.03f, 0.02f, 0.05f, DimAlpha()));
            float pw = 460, px = (w - pw) / 2, py = h * 0.5f - Ph / 2 + Drop.Value * 420;
            Theme.Panel(b, px, py, pw, Ph);
            Theme.TextCentered(font, b, "Jeda", w / 2, py + 34, Theme.Body, Theme.Cream);
            b.Rect(w / 2 - 70, py + 80, 140, 3, Theme.Gold);
            menu.Draw(Game, b, font);
            Theme.TextCentered(font, b, "G Tas   Q Misi   C Catatan   F5 Simpan", w / 2, py + Ph - 50, Theme.Small + 4, Theme.Cream);
        }
    }

    public sealed class InventoryScene : OverlayScene
    {
        private const int Cols = 6, Rows = 3;
        private const float Cell = 78, CellGap = 8;
        private readonly PlayScene play;
        private ButtonStack closeBtn = null!;

        public InventoryScene(PlayScene play) => this.play = play;

        public override void OnEnter() => closeBtn = new ButtonStack(new[] { "Tutup" }, 200, 54);

        public override void Update(float dt)
        {
            Tick(dt);
            if (HandleClosing()) return;
            if (Game.Input.Pressed(InputAction.Cancel) || Game.Input.Pressed(InputAction.Inventory) || closeBtn.Update(Game, dt) != null) Close();
        }

        public override void Draw(Renderer gfx)
        {
            var b = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            b.Rect(0, 0, w, h, new Rgba(0.03f, 0.02f, 0.05f, DimAlpha()));
            float pw = Cols * Cell + (Cols - 1) * CellGap + 120, ph = Rows * Cell + (Rows - 1) * CellGap + 270;
            float px = (w - pw) / 2, py = (h - ph) / 2 - 20 + Drop.Value * 420;
            Theme.Panel(b, px, py, pw, ph, kind: "paper");
            Theme.InkText(font, b, "Tas", px + 44, py + 34, Theme.Body);
            var coin = Game.Atlas["icon_coin"];
            string label = play.Inv.Gold.ToString();
            float lx = px + pw - 44 - font.Measure(label, Theme.Ui);
            b.Quad(coin, lx - coin.W * 2 - 8, py + 34, coin.W * 2, coin.H * 2);
            Theme.InkText(font, b, label, lx, py + 38, Theme.Ui);
            b.Rect(px + 44, py + 84, pw - 88, 3, Theme.InkDim.WithA(0.55f));
            var items = play.Items.All.ToList();
            float gx0 = px + 60, gy0 = py + 108;
            var (mx, my) = Game.MouseUi();
            ItemDef? hovered = null;
            for (int n = 0; n < Cols * Rows; n++)
            {
                float x = gx0 + n % Cols * (Cell + CellGap), y = gy0 + n / Cols * (Cell + CellGap);
                Theme.Slot(b, x, y, Cell);
                if (n >= items.Count) continue;
                var item = items[n];
                int count = play.Inv.Count(item.Id);
                bool inside = x <= mx && mx <= x + Cell && y <= my && my <= y + Cell;
                if (inside) { hovered = item; b.Rect(x + 3, y + 3, Cell - 6, Cell - 6, new Rgba(1, 1, 1, 0.10f)); }
                b.Quad(Game.Atlas[item.Icon], x + (Cell - 48) / 2, y + (Cell - 48) / 2 - (inside ? 4 : 0), 48, 48, new Rgba(1, 1, 1, count > 0 ? 1 : 0.35f));
                if (count > 0)
                {
                    // bintang kualitas hasil panen (versi C#): titik emas kecil per tingkat terbaik yang dimiliki
                    var bd = play.Inv.Breakdown(item.Id);
                    int best = bd.Where(kv => kv.Value > 0).Select(kv => kv.Key).DefaultIfEmpty(1).Max();
                    if (item.Sellable) for (int s = 0; s < best; s++) b.Quad(Game.Atlas["ui_star"], x + 6 + s * 14, y + 6, 14, 14);
                    string s2 = count.ToString();
                    Theme.Text(font, b, s2, x + Cell - 8 - font.Measure(s2, Theme.Small), y + Cell - 22, Theme.Small, Theme.Cream);
                }
            }
            closeBtn.Place(w / 2, py + ph - 100);
            closeBtn.Draw(Game, b, font);
            if (hovered != null)
            {
                string price = hovered.Sellable ? $"Jual: {hovered.Sell} keping" : $"Beli: {hovered.Buy} keping";
                Theme.Tooltip(b, font, mx, my, w, h, hovered.Name, $"{hovered.Desc}\n\n{price}");
            }
        }
    }

    public sealed class QuestScene : OverlayScene
    {
        private const float Pw = 1040, Ph = 600, LeftW = 410, CardH = 112;
        private readonly PlayScene play;
        private ButtonStack closeBtn = null!;

        public QuestScene(PlayScene play) => this.play = play;

        public override void OnEnter() => closeBtn = new ButtonStack(new[] { "Tutup" }, 180, 52);

        private (float X, float Y) Origin()
        {
            var (w, h) = Game.Gfx.UiSize;
            return ((w - Pw) / 2, (h - Ph) / 2 + Drop.Value * 420);
        }

        public static void Bar(SpriteBatch b, float x, float y, float w, float h, float frac, Rgba? color = null)
        {
            var c = color ?? Theme.Leaf;
            b.Rect(x - 3, y - 3, w + 6, h + 6, Theme.Outline);
            b.Rect(x, y, w, h, new Rgba(0.20f, 0.14f, 0.10f));
            float fw = MathF.Round(w * Ease.Clamp01(frac));
            if (fw > 0)
            {
                b.Rect(x, y, fw, h, c);
                b.Rect(x, y, fw, Math.Max(2, (int)h / 4), new Rgba(Math.Min(1, c.R + 0.2f), Math.Min(1, c.G + 0.2f), Math.Min(1, c.B + 0.2f)));
            }
        }

        public override void Update(float dt)
        {
            Tick(dt);
            if (HandleClosing()) return;
            var (px, py) = Origin();
            closeBtn.Place(px + Pw - 40 - 90, py + Ph - 28 - 52);
            var inp = Game.Input;
            if (inp.Pressed(InputAction.Cancel) || inp.Pressed(InputAction.Quests) || closeBtn.Update(Game, dt) != null) Close();
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
            DrawDebt(b, font, px + 44, py + 34);
            b.Rect(px + 44 + LeftW + 20, py + 40, 3, Ph - 130, Theme.InkDim.WithA(0.4f));
            DrawQuests(b, font, px + 44 + LeftW + 52, py + 34, px);
            closeBtn.Draw(Game, b, font);
            Theme.InkText(font, b, "Q / Esc: tutup", px + 44, py + Ph - 44, Theme.Small + 4, Theme.InkDim);
        }

        private void DrawDebt(SpriteBatch b, BitmapFont font, float x, float y)
        {
            var d = play.Debt;
            var st = Game.State;
            Theme.InkText(font, b, play.Story.Title, x, y - 22, Theme.Small + 2, new Rgba(0.55f, 0.36f, 0.12f));
            Theme.InkText(font, b, "Utang Warisan", x, y, Theme.Body);
            if (d.Free)
            {
                Theme.InkText(font, b, "LUNAS!", x, y + 64, Theme.Title, new Rgba(0.30f, 0.55f, 0.22f));
                Theme.InkText(font, b, "Kamu bebas dari utang.", x, y + 134, Theme.Ui);
            }
            else
            {
                string rem = Fmt.N(d.Remaining);
                Theme.InkText(font, b, rem, x, y + 56, Theme.Title, new Rgba(0.62f, 0.20f, 0.18f));
                Theme.InkText(font, b, "keping tersisa", x + font.Measure(rem, Theme.Title) + 14, y + 90, Theme.Ui, Theme.InkDim);
                Bar(b, x, y + 142, LeftW, 26, (float)d.PaidFraction);
                Theme.InkText(font, b, $"Terbayar {Fmt.N(st.GetInt("debt_paid"))} dari {Fmt.N(st.GetInt("debt_start", 6000))}", x, y + 178, Theme.Small + 4, Theme.InkDim);
                float yy = y + 218;
                foreach (var (text, col) in new[]
                {
                    ($"Bunga: {Math.Round(d.Rate * 100)}% per musim", Theme.Ink),
                    ($"Cicilan musim ini: {Fmt.N(d.SeasonPaid)} / {Fmt.N(d.Minimum)}", d.SeasonLeft == 0 ? new Rgba(0.30f, 0.50f, 0.22f) : Theme.Ink),
                    ($"Penagih datang: {Debt.DaysUntilVisit(Game.Clock.Day)} hari lagi", Theme.Ink),
                })
                {
                    Theme.InkText(font, b, text, x, yy, Theme.Ui, col);
                    yy += 32;
                }
            }
            float by = y + 330;
            Theme.Panel(b, x - 6, by, LeftW + 12, 160, shadow: false);
            if (st.Get("last_lesson") is System.Text.Json.Nodes.JsonArray lesson && lesson.Count == 2)
            {
                Theme.Text(font, b, $"Pelajaran: {(string)lesson[0]!}", x + 14, by + 16, Theme.Small + 4, Theme.Gold);
                var lines = font.Wrap((string)lesson[1]!, Theme.Small + 4, LeftW - 40);
                for (int i = 0; i < Math.Min(5, lines.Count); i++) Theme.Text(font, b, lines[i], x + 14, by + 46 + i * 22, Theme.Small + 4, Theme.Cream, outline: false);
            }
            else Theme.Text(font, b, "Selesaikan misi untuk membuka pelajaran bertani dan keuangan.", x + 14, by + 20, Theme.Small + 4, Theme.Cream, outline: false);
        }

        private void DrawQuests(SpriteBatch b, BitmapFont font, float x, float y, float originX)
        {
            var qs = play.Quests;
            float rightW = Pw - 44 - (x - originX) - 20;
            Theme.InkText(font, b, "Misi Aktif", x, y, Theme.Body);
            string count = $"{qs.Done().Count} / {qs.Defs.Count} selesai";
            Theme.InkText(font, b, count, x + rightW - font.Measure(count, Theme.Small + 4), y + 12, Theme.Small + 4, Theme.InkDim);
            var active = qs.Active().Take(4).ToList();
            if (active.Count == 0) Theme.InkText(font, b, "Semua misi tersedia sudah selesai.", x, y + 70, Theme.Ui, Theme.InkDim);
            for (int i = 0; i < active.Count; i++)
            {
                var q = active[i];
                float cy = y + 62 + i * (CardH + 8);
                Theme.Button(b, x - 10, cy, rightW + 20, CardH);
                Theme.Text(font, b, q.Title, x + 8, cy + 10, Theme.Ui, Theme.Gold);
                Theme.Text(font, b, q.Giver, x + rightW - font.Measure(q.Giver, Theme.Small), cy + 16, Theme.Small, Theme.Dim);
                var desc = font.Wrap(q.Desc, Theme.Small + 4, rightW - 20);
                for (int j = 0; j < Math.Min(2, desc.Count); j++) Theme.Text(font, b, desc[j], x + 8, cy + 42 + j * 20, Theme.Small + 4, Theme.Cream, outline: false);
                for (int j = 0; j < Math.Min(2, q.Objectives.Length); j++)
                {
                    var (cur, n) = qs.Progress(q, j);
                    float bw = rightW - 300;
                    Bar(b, x + 8, cy + 88, bw, 14, cur / (float)Math.Max(1, n));
                    Theme.Text(font, b, $"{Fmt.N(cur)}/{Fmt.N(n)} {q.Objectives[j].Label}", x + 8 + bw + 12, cy + 84, Theme.Small, Theme.Cream);
                }
                var rewards = new List<string>();
                if (q.Reward.Gold > 0) rewards.Add($"{Fmt.N(q.Reward.Gold)} keping");
                if (q.Reward.Rings > 0) rewards.Add("wilayah baru");
                if (q.Reward.Items.Count > 0) rewards.Add("benih");
                if (rewards.Count > 0)
                {
                    string text = "Hadiah: " + string.Join(", ", rewards);
                    Theme.Text(font, b, text, x + rightW - font.Measure(text, Theme.Small), cy + 64, Theme.Small, Theme.Leaf);
                }
            }
        }
    }

    /// <summary>Duduk sejenak: HUD memudar, letterbox masuk, waktu berjalan lebih cepat, satu pikiran muncul.</summary>
    public sealed class SitScene : Scene
    {
        private const float TimeSpeed = 8, BarH = 78;
        private readonly PlayScene play;
        private float t, ct;
        private bool closing;
        private List<string> lines = new();

        public SitScene(PlayScene play) => this.play = play;

        public override bool Transparent => true;

        public override void OnEnter()
        {
            play.Cinematic = true;
            play.Player.Moving = false;
            play.Player.Facing = Facing.Down;
            var rng = new Random((int)(Game.Clock.Day * 977 + Game.Clock.Minutes));
            lines = Game.Font.Wrap(play.Story.Thought(Game.Clock.Minutes, rng), Theme.Ui, 860);
        }

        public override void OnExit() => play.Cinematic = false;

        public override void Update(float dt)
        {
            var inp = Game.Input;
            t += dt;
            play.Time += dt;
            play.Atmo.Update(dt, Game.Clock.Minutes);
            play.Particles.Update(dt);
            var (px, py) = play.Player.WorldPosition;
            play.Camera.Follow(px, py, 3, dt);
            if (Game.Clock.Minutes < GameClock.DayEnd - 40) Game.Clock.Update(dt * TimeSpeed);
            if (closing)
            {
                ct += dt;
                if (ct > 0.45f) Game.Scenes.Pop();
                return;
            }
            if (t > 1 && new[] { InputAction.Cancel, InputAction.Interact, InputAction.Confirm, InputAction.Left, InputAction.Right, InputAction.Up, InputAction.Down }.Any(inp.Pressed))
                closing = true;
        }

        public override void Draw(Renderer gfx)
        {
            var b = gfx.Batch;
            var font = Game.Font;
            gfx.ViewUi();
            var (w, h) = gfx.UiSize;
            float k = Ease.OutCubic(Ease.Clamp01(t / 0.8f)) * (1 - Ease.OutCubic(Ease.Clamp01(ct / 0.45f)));
            b.Rect(0, 0, w, BarH * k, new Rgba(0.02f, 0.02f, 0.03f));
            b.Rect(0, h - BarH * k, w, BarH * k, new Rgba(0.02f, 0.02f, 0.03f));
            float a = Ease.Clamp01((t - 1.2f) / 1.2f) * (1 - Ease.Clamp01(ct / 0.3f));
            float y = h - BarH - 30 - lines.Count * 34;
            for (int i = 0; i < lines.Count; i++) Theme.TextCentered(font, b, lines[i], w / 2, y + i * 34, Theme.Ui, Theme.Cream, a);
            Theme.TextCentered(font, b, Game.Clock.ClockLabel, w / 2, 22 * k + 6, Theme.Small + 4, Theme.Gold, a * 0.9f);
            Theme.TextCentered(font, b, "E / Esc: berdiri", w / 2, h - BarH * k + 26, Theme.Small + 2, Theme.Dim, a * 0.8f);
        }
    }
}
