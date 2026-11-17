using Tandur.Core;
using Tandur.Engine;
using Tandur.World;

namespace Tandur.Ui
{
    /// <summary>
    /// HUD gameplay: jam berbentuk dial langit + emas (kanan atas), tabung energi (kanan bawah), hotbar alat
    /// (tengah bawah). Stateful agar angka bergulir, slot memantul, dan bar energi meninggalkan jejak.
    /// </summary>
    public sealed class Hud
    {
        public const float SlotSize = 66, Gap = 6, Pad = 18;
        private const float DialW = 46, DialH = 25, SunR = 15, DialCx = 22.5f, DialCy = 23.5f, BarW = 14, BarH = 44;
        private const int S = Theme.Scale;

        private readonly Spring shake = new(0, 500, 11);
        private readonly List<Spring> icons = new();
        private float t, nameT = 99, energyFlash;
        private int selected = -1;

        public Counter Coin { get; } = new();
        public float EnergyTrail { get; set; } = 1;
        public float EnergyLast { get; set; } = 1;
        public string Weather { get; set; } = "";

        public void Update(float dt, int gold, float energyFrac, int sel, int count)
        {
            t += dt;
            Coin.Set(gold);
            Coin.Update(dt);
            shake.Target = 0;
            shake.Update(dt);
            while (icons.Count < count) icons.Add(new Spring(0, 380, 13));
            if (sel != selected)
            {
                if (selected >= 0) icons[sel].Kick(-190);
                selected = sel;
                nameT = 0;
            }
            foreach (var s in icons) s.Update(dt);
            nameT += dt;
            if (energyFrac < EnergyLast - 1e-6f) energyFlash = 0.35f;
            EnergyLast = energyFrac;
            EnergyTrail = energyFrac >= EnergyTrail ? energyFrac : Math.Max(energyFrac, EnergyTrail - dt * 0.35f);
            energyFlash = Math.Max(0, energyFlash - dt);
        }

        public void Exhausted() => shake.Kick(900);

        public static ((float X, float Y, float W, float H) Panel, List<(float X, float Y, float W, float H)> Slots) HotbarGeometry(float uiW, float uiH, int n)
        {
            float inner = n * SlotSize + (n - 1) * Gap;
            float pw = inner + 2 * Pad + 6, ph = SlotSize + 2 * Pad + 2;
            float px = (uiW - pw) / 2, py = uiH - ph - 14;
            var slots = Enumerable.Range(0, n).Select(i => (px + Pad + 3 + i * (SlotSize + Gap), py + Pad + 1, SlotSize, SlotSize)).ToList();
            return ((px, py, pw, ph), slots);
        }

        public static (float X, float Y, float W, float H) EnergyGeometry(float uiW, float uiH)
        {
            float w = BarW * S, h = BarH * S;
            return (uiW - 22 - w, uiH - 24 - h, w, h);
        }

        public int? SlotAt(float mx, float my, float uiW, float uiH, int n)
        {
            var (_, slots) = HotbarGeometry(uiW, uiH, n);
            for (int i = 0; i < slots.Count; i++)
            {
                var (x, y, w, h) = slots[i];
                if (x <= mx && mx <= x + w && y <= my && my <= y + h) return i;
            }
            return null;
        }

        public bool OverUi(float mx, float my, float uiW, float uiH, int n)
        {
            var ((px, py, pw, ph), _) = HotbarGeometry(uiW, uiH, n);
            if (px <= mx && mx <= px + pw && py <= my && my <= py + ph) return true;
            var (ex, ey, ew, eh) = EnergyGeometry(uiW, uiH);
            return ex - 10 <= mx && mx <= ex + ew + 10 && ey - 40 <= my && my <= ey + eh + 10;
        }

        public void Draw(Renderer gfx, BitmapFont font, Atlas atlas, GameClock clock, int energy, int energyMax,
                         List<FarmTool> hotbar, int sel, Dictionary<string, int> counts, (float X, float Y) mouse, float hintAlpha = 0)
        {
            var batch = gfx.Batch;
            var (uiW, uiH) = gfx.UiSize;
            DrawClock(batch, font, atlas, uiW, clock);
            DrawEnergy(batch, atlas, uiW, uiH, energy, energyMax);
            DrawHotbar(batch, font, atlas, uiW, uiH, hotbar, sel, counts, mouse);
            if (hintAlpha > 0.01f)
            {
                string[] lines =
                {
                    "WASD  jalan        Shift  lari",
                    "Klik / Spasi  pakai alat        1-5 / roda  ganti alat",
                    "E  bicara / duduk        G  tas   Q  misi   C  catatan   Esc  jeda",
                };
                for (int i = 0; i < lines.Length; i++) Theme.Text(font, batch, lines[i], 22, uiH - 118 + i * 22, Theme.Small, Theme.Cream, hintAlpha);
            }
        }

        public void Tooltip(Renderer gfx, BitmapFont font, List<FarmTool> hotbar, Dictionary<string, int> counts, (float X, float Y) mouse, int energy, int energyMax)
        {
            var (uiW, uiH) = gfx.UiSize;
            var (mx, my) = mouse;
            if (SlotAt(mx, my, uiW, uiH, hotbar.Count) is int i)
            {
                var e = hotbar[i];
                string body = e.Desc + (e.Item != null ? $"\n\nDimiliki: {counts.GetValueOrDefault(e.Item)}" : "");
                Theme.Tooltip(gfx.Batch, font, mx, my, uiW, uiH, e.Label, body);
                return;
            }
            var (ex, ey, ew, eh) = EnergyGeometry(uiW, uiH);
            if (ex - 6 <= mx && mx <= ex + ew + 6 && ey - 40 <= my && my <= ey + eh + 6)
                Theme.Tooltip(gfx.Batch, font, mx, my, uiW, uiH, "Energi", $"{energy} / {energyMax}\nCangkul, siram, dan menanam menguras tenaga. Tidur memulihkannya.");
        }

        private void DrawClock(SpriteBatch batch, BitmapFont font, Atlas atlas, float uiW, GameClock clock)
        {
            float w = 316, h = 108, x = uiW - w - 20, y = 16;
            Theme.Panel(batch, x, y, w, h);
            float dx = x + 20, dy = y + 17;
            batch.Quad(atlas["ui_dial"], dx, dy, DialW * S, DialH * S);
            float tt = Ease.Clamp01((clock.Minutes - GameClock.DayStart) / (24 * 60 - GameClock.DayStart));
            float ang = MathF.PI * tt;
            float sx = DialCx - SunR * MathF.Cos(ang), sy = DialCy - SunR * MathF.Sin(ang);
            var orb = atlas[tt < 0.72f ? "ui_sun" : "ui_moon"];
            batch.Quad(orb, dx + (sx - orb.W / 2f + 0.5f) * S, dy + (sy - orb.H / 2f + 0.5f) * S, orb.W * S, orb.H * S);
            batch.Quad(atlas["ui_dial_front"], dx, dy, DialW * S, DialH * S);

            float cx = dx + DialW * S + 14;
            bool late = clock.Minutes > 22 * 60;
            float pulse = late ? 0.5f + 0.5f * MathF.Sin(t * 6) : 0;
            Theme.Text(font, batch, clock.ClockLabel, cx, y + 18, Theme.Ui, Rgba.Lerp(Theme.Cream, Theme.Berry, pulse));
            Theme.Text(font, batch, $"Hari {clock.DayOfSeason}/{GameClock.DaysPerSeason}", cx, y + 48, Theme.Small, Theme.Gold);
            var si = atlas[$"icon_season_{clock.Season.ToLowerInvariant()}"];
            batch.Quad(si, cx, y + 68, si.W * 2, si.H * 2);
            Theme.Text(font, batch, clock.Season + (Weather.Length > 0 ? $" | {Weather}" : ""), cx + si.W * 2 + 6, y + 70, Theme.Small, Theme.Dim);

            float gw = 176, gh = 54, gx = x + w - gw, gy = y + h + 2;
            Theme.Panel(batch, gx, gy, gw, gh);
            var coin = atlas["icon_coin"];
            float bob = Coin.Pop.Value;
            batch.Quad(coin, gx + 20, gy + 11 - bob * 0.3f, coin.W * 2, coin.H * 2);
            Theme.Text(font, batch, Coin.Text, gx + 64, gy + 12 - bob * 0.4f, Theme.Ui, Theme.Cream);
            if (Coin.DeltaAge < 1.4f && Coin.Delta != 0)
            {
                float a = Ease.Clamp01(1.4f - Coin.DeltaAge), up = Ease.OutBack(Coin.DeltaAge / 0.5f) * 14;
                bool good = Coin.Delta > 0;
                Theme.Text(font, batch, $"{(good ? "+" : "")}{Coin.Delta}", gx + 64 + 16 + font.Measure(Coin.Text, Theme.Ui), gy + 14 - up, Theme.Small + 8, good ? Theme.Leaf : Theme.Berry, a);
            }
        }

        private void DrawEnergy(SpriteBatch batch, Atlas atlas, float uiW, float uiH, int energy, int energyMax)
        {
            var (ex, ey, ew, eh) = EnergyGeometry(uiW, uiH);
            ex += shake.Value * 0.01f;
            float frac = Ease.Clamp01(energy / (float)energyMax);
            var bolt = atlas["icon_energy"];
            float pulse = 1 + (frac < 0.2f ? 0.06f * MathF.Sin(t * 8) : 0);
            batch.Quad(bolt, ex + (ew - bolt.W * S) / 2, ey - bolt.H * S - 6, bolt.W * S * pulse, bolt.H * S * pulse, frac > 0.2f ? Rgba.White : new Rgba(1, 0.6f, 0.55f));
            Theme.Panel(batch, ex - 9, ey - 9, ew + 18, eh + 18);
            Theme.Nine(batch, "ui_barframe", ex, ey, ew, eh);
            float ix = ex + 4 * S, iy = ey + 4 * S, iw = ew - 8 * S, ih = eh - 8 * S;
            int rows = (int)(ih / S);
            int fillRows = (int)MathF.Ceiling(frac * rows - 1e-6f), trailRows = (int)MathF.Ceiling(EnergyTrail * rows - 1e-6f);
            var main = frac > 0.5f ? new Rgba(0.45f, 0.82f, 0.32f) : frac > 0.25f ? new Rgba(0.95f, 0.78f, 0.25f) : new Rgba(0.9f, 0.3f, 0.25f);
            for (int r = 0; r < trailRows; r++)
            {
                float ry = iy + ih - (r + 1) * S;
                if (r < fillRows)
                {
                    var c = r % 6 == 5 ? main.Mul(0.82f) : main;
                    if (energyFlash > 0) c = new Rgba(Math.Min(1, c.R + 0.35f), Math.Min(1, c.G + 0.35f), Math.Min(1, c.B + 0.35f));
                    batch.Rect(ix, ry, iw, S, c);
                    batch.Rect(ix, ry, S, S, new Rgba(Math.Min(1, c.R + 0.25f), Math.Min(1, c.G + 0.25f), Math.Min(1, c.B + 0.25f)));
                    batch.Rect(ix + iw - S, ry, S, S, c.Mul(0.7f));
                }
                else batch.Rect(ix, ry, iw, S, new Rgba(0.95f, 0.88f, 0.8f, 0.85f));
            }
            int shimmer = (int)(t * 22) % (rows + 14) - 7;
            for (int k = 0; k < 2; k++)
            {
                int r = shimmer + k;
                if (r >= 0 && r < fillRows) batch.Rect(ix, iy + ih - (r + 1) * S, iw, S, new Rgba(1, 1, 1, 0.22f));
            }
        }

        private void DrawHotbar(SpriteBatch batch, BitmapFont font, Atlas atlas, float uiW, float uiH, List<FarmTool> hotbar, int sel, Dictionary<string, int> counts, (float X, float Y) mouse)
        {
            var ((px, py, pw, ph), slots) = HotbarGeometry(uiW, uiH, hotbar.Count);
            Theme.Panel(batch, px, py, pw, ph);
            var (mx, my) = mouse;
            for (int i = 0; i < hotbar.Count; i++)
            {
                var e = hotbar[i];
                var (x, y, w, h) = slots[i];
                bool isSel = i == sel;
                Theme.Slot(batch, x, y, w, isSel);
                bool hovered = x <= mx && mx <= x + w && y <= my && my <= y + h;
                int? count = e.Item != null ? counts.GetValueOrDefault(e.Item) : null;
                float dim = e.Item != null && count == 0 ? 0.4f : 1;
                float bounce = i < icons.Count ? icons[i].Value * 0.05f : 0;
                float lift = hovered && !isSel ? -4 : 0;
                batch.Quad(atlas[e.Icon], x + (w - 16 * S) / 2, y + (h - 16 * S) / 2 + 1 + bounce + lift, 16 * S, 16 * S, new Rgba(1, 1, 1, dim));
                if (hovered) batch.Rect(x + 3, y + 3, w - 6, h - 6, new Rgba(1, 1, 1, 0.10f));
                Theme.Text(font, batch, (i + 1).ToString(), x + 6, y + 3, Theme.Small, isSel ? Theme.Gold : Theme.Dim);
                if (count is int c)
                {
                    string label = c.ToString();
                    Theme.Text(font, batch, label, x + w - 7 - font.Measure(label, Theme.Small), y + h - 20, Theme.Small, c > 0 ? Theme.Cream : Theme.Berry);
                }
            }
            if (nameT < 1.6f && sel >= 0 && sel < hotbar.Count)
            {
                float a = Ease.Clamp01(Math.Min(nameT / 0.12f, (1.6f - nameT) / 0.4f));
                string label = hotbar[sel].Label;
                float tw = font.Measure(label, Theme.Ui) + 40, rise = (1 - Ease.OutBack(nameT / 0.25f)) * 14;
                Theme.Panel(batch, uiW / 2 - tw / 2, py - 56 + rise, tw, 46, a, shadow: false);
                Theme.TextCentered(font, batch, label, uiW / 2, py - 56 + rise + 12, Theme.Ui, Theme.Gold, a);
            }
        }
    }
}
