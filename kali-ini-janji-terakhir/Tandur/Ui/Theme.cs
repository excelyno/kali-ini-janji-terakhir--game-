using Tandur.Engine;

namespace Tandur.Ui
{
    /// <summary>Satu tema visual untuk semua layar: kayu rumah tani untuk HUD, perkamen untuk dialog, aksen emas.</summary>
    public static class Theme
    {
        public const int Small = 16, Ui = 24, Body = 32, Title = 56;
        public const int Scale = 3;

        public static readonly Rgba Outline = new(0.11f, 0.06f, 0.03f);
        public static readonly Rgba Wood = new(0.29f, 0.18f, 0.11f);
        public static readonly Rgba WoodHi = new(0.43f, 0.28f, 0.17f);
        public static readonly Rgba Gold = new(0.98f, 0.78f, 0.33f);
        public static readonly Rgba Cream = new(1.00f, 0.95f, 0.82f);
        public static readonly Rgba Dim = new(0.80f, 0.70f, 0.54f);
        public static readonly Rgba Ink = new(0.24f, 0.14f, 0.08f);
        public static readonly Rgba InkDim = new(0.45f, 0.32f, 0.20f);
        public static readonly Rgba Leaf = new(0.52f, 0.80f, 0.38f);
        public static readonly Rgba Berry = new(0.90f, 0.38f, 0.32f);
        public static readonly Rgba Sky = new(0.55f, 0.78f, 0.92f);

        private static readonly Dictionary<string, int> Borders = new()
        {
            ["ui_wood"] = 6, ["ui_paper"] = 6, ["ui_slot"] = 3, ["ui_slot_sel"] = 3, ["ui_btn"] = 6, ["ui_btn_hover"] = 6,
            ["ui_btn_down"] = 6, ["ui_barframe"] = 4, ["ui_keycap"] = 4,
        };

        /// <summary>9-slice dengan tepi yang di-tile (tidak pernah diregangkan) agar piksel seni tetap persegi.</summary>
        public static (float W, float H) Nine(SpriteBatch batch, string name, float x, float y, float w, float h, Rgba? color = null, int scale = Scale)
        {
            var c = color ?? Rgba.White;
            var region = batch.Atlas[name];
            int b = Borders[name];
            int aw = Math.Max(2 * b + 1, (int)MathF.Round(w / scale)), ah = Math.Max(2 * b + 1, (int)MathF.Round(h / scale));
            x = MathF.Round(x); y = MathF.Round(y);
            int cw = region.W - 2 * b, ch = region.H - 2 * b;
            float farX = x + (aw - b) * scale, farY = y + (ah - b) * scale;
            batch.Quad(region.Sub(0, 0, b, b), x, y, b * scale, b * scale, c);
            batch.Quad(region.Sub(region.W - b, 0, b, b), farX, y, b * scale, b * scale, c);
            batch.Quad(region.Sub(0, region.H - b, b, b), x, farY, b * scale, b * scale, c);
            batch.Quad(region.Sub(region.W - b, region.H - b, b, b), farX, farY, b * scale, b * scale, c);
            for (int ox = b; ox < aw - b; ox += cw)
            {
                int tw = Math.Min(cw, aw - b - ox);
                float px = x + ox * scale;
                batch.Quad(region.Sub(b, 0, tw, b), px, y, tw * scale, b * scale, c);
                batch.Quad(region.Sub(b, region.H - b, tw, b), px, farY, tw * scale, b * scale, c);
                for (int oy = b; oy < ah - b; oy += ch)
                {
                    int th = Math.Min(ch, ah - b - oy);
                    batch.Quad(region.Sub(b, b, tw, th), px, y + oy * scale, tw * scale, th * scale, c);
                }
            }
            for (int oy = b; oy < ah - b; oy += ch)
            {
                int th = Math.Min(ch, ah - b - oy);
                float py = y + oy * scale;
                batch.Quad(region.Sub(0, b, b, th), x, py, b * scale, th * scale, c);
                batch.Quad(region.Sub(region.W - b, b, b, th), farX, py, b * scale, th * scale, c);
            }
            return (aw * scale, ah * scale);
        }

        public static (float W, float H) Panel(SpriteBatch batch, float x, float y, float w, float h, float alpha = 1, string kind = "wood", bool shadow = true, Rgba? tint = null)
        {
            var t = tint ?? Rgba.White;
            string sprite = kind == "paper" ? "ui_paper" : "ui_wood";
            if (shadow) Nine(batch, sprite, x + 3, y + 7, w, h, new Rgba(0, 0, 0, 0.28f * alpha));
            return Nine(batch, sprite, x, y, w, h, t.WithA(alpha));
        }

        public static void Slot(SpriteBatch b, float x, float y, float size, bool selected = false, float alpha = 1)
            => Nine(b, selected ? "ui_slot_sel" : "ui_slot", x, y, size, size, new Rgba(1, 1, 1, alpha));

        public static void Button(SpriteBatch b, float x, float y, float w, float h, string state = "normal", float alpha = 1)
            => Nine(b, state switch { "hover" => "ui_btn_hover", "down" => "ui_btn_down", _ => "ui_btn" }, x, y, w, h, new Rgba(1, 1, 1, alpha));

        public static void Frame(SpriteBatch b, float x, float y, float w, float h, Rgba color, float alpha = 1, float t = 3)
        {
            var c = color.WithA(alpha);
            b.Rect(x - t, y - t, w + 2 * t, t, c);
            b.Rect(x - t, y + h, w + 2 * t, t, c);
            b.Rect(x - t, y, t, h, c);
            b.Rect(x + w, y, t, h, c);
        }

        /// <summary>Teks dengan garis luar gelap + bayangan jatuh (terbaca di latar apa pun).</summary>
        public static void Text(BitmapFont f, SpriteBatch b, string s, float x, float y, float size, Rgba color, float alpha = 1, bool outline = true)
        {
            if (outline)
            {
                float o = size >= Ui ? 2 : 1;
                var dark = Outline.WithA(alpha);
                foreach (var (dx, dy) in new[] { (-o, 0f), (o, 0f), (0f, -o), (0f, o), (o, o) }) f.Draw(b, s, x + dx, y + dy, size, dark);
            }
            f.Draw(b, s, x, y, size, color.WithA(alpha));
        }

        public static void TextCentered(BitmapFont f, SpriteBatch b, string s, float cx, float y, float size, Rgba color, float alpha = 1, bool outline = true)
            => Text(f, b, s, cx - f.Measure(s, size) / 2, y, size, color, alpha, outline);

        /// <summary>Teks gelap polos untuk perkamen.</summary>
        public static void InkText(BitmapFont f, SpriteBatch b, string s, float x, float y, float size, Rgba? color = null, float alpha = 1)
            => f.Draw(b, s, x, y, size, (color ?? Ink).WithA(alpha));

        public static void Tooltip(SpriteBatch batch, BitmapFont font, float mx, float my, float uiW, float uiH, string title, string? body = null, Rgba? color = null)
        {
            var lines = body != null ? font.Wrap(body, Small, 270) : new List<string>();
            float tw = Math.Max(font.Measure(title, Ui), lines.Count > 0 ? lines.Max(l => font.Measure(l, Small)) : 0);
            float w = tw + 2 * 18 + 16;
            float h = 18 * 2 + 12 + 24 + (lines.Count > 0 ? lines.Count * (font.LineHeight(Small) + 2) + 6 : 0);
            float x = mx + 26, y = my + 30;
            if (x + w > uiW - 8) x = mx - w - 12;
            if (y + h > uiH - 8) y = my - h - 8;
            Panel(batch, x, y, w, h, kind: "paper");
            InkText(font, batch, title, x + 26, y + 20, Ui, color ?? Ink);
            float ty = y + 50;
            foreach (var l in lines) { InkText(font, batch, l, x + 26, ty, Small, InkDim); ty += font.LineHeight(Small) + 2; }
        }
    }
}
