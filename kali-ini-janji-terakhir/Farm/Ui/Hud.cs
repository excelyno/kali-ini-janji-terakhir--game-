using System;
using System.Drawing;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Farm.Ui
{
    /// <summary>
    /// HUD ala Stardew Valley:
    ///   kanan atas  : jam, hari, musim, cuaca, koin
    ///   kiri atas   : misi aktif dan cicilan utang
    ///   bawah tengah: hotbar alat
    ///   kanan bawah : bar energi dan air (tegak)
    ///   atas hotbar : petunjuk aksi [E]
    /// </summary>
    public static class Hud
    {
        public const int SlotSize = 52;

        public static void Draw(Renderer r, int vw, int vh, FarmWorld w, bool showPrompt)
        {
            DrawClock(r, vw, w);
            DrawQuest(r, w);
            int hotbarTop = DrawHotbar(r, vw, vh, w);
            DrawBars(r, vw, vh, w);
            if (showPrompt) DrawPrompt(r, vw, hotbarTop, w);
            DrawMessage(r, vw, w);
            r.Text("WASD gerak  Shift lari  Spasi/klik alat  E/klik kanan aksi  1-9 alat  Q jurnal  F3 hitbox  Esc jeda",
                Fonts.Small, Color.White, 12, vh - 20, alpha: 0.8f);
        }

        private static void DrawClock(Renderer r, int vw, FarmWorld w)
        {
            var c = w.Clock;
            int x = vw - 232, y = 10;
            r.DrawFrame("ui_frame", x, y, 222, 124);
            r.Draw($"ui_season_{GameClock.SeasonName(c.Season).ToLowerInvariant()}", x + 18, y + 18);
            r.Text($"Hari {c.DayOfSeason}, {GameClock.SeasonName(c.Season)}", Fonts.Large, Theme.Ink, x + 44, y + 16, shadow: false);
            r.Draw($"ui_weather_{Weather.Name(w.Weather).ToLowerInvariant()}", x + 16, y + 50);
            r.Text(c.TimeText, Fonts.Large, Theme.Ink, x + 52, y + 50, shadow: false);
            r.Text("Th." + c.Year, Fonts.Small, Theme.InkSoft, x + 150, y + 55, shadow: false);

            r.DrawFrame("ui_frame_dark", x + 10, y + 80, 202, 34);
            r.Draw("ui_icon_coin", x + 20, y + 88);
            r.Text(w.Player.Wallet.Coins.ToString("N0"), Fonts.Large, Theme.Gold, x + 46, y + 86);
        }

        private static void DrawQuest(Renderer r, FarmWorld w)
        {
            int x = 10, y = 10, width = 340;
            r.DrawFrame("ui_frame_dark", x, y, width, 112);
            var q = w.Quests.Active;
            if (q == null)
            {
                r.Text("Semua misi selesai!", Fonts.Large, Theme.Good, x + 18, y + 18);
            }
            else
            {
                r.Draw("ui_icon_quest", x + 16, y + 16);
                r.Text(q.Title, Fonts.Large, Theme.Gold, x + 42, y + 14);
                var lines = r.Wrap(q.Description, Fonts.Small, width - 36);
                for (int i = 0; i < Math.Min(2, lines.Count); i++)
                    r.Text(lines[i], Fonts.Small, Theme.Light, x + 18, y + 40 + i * 15);
                r.DrawBar(new Rectangle(x + 16, y + 74, width - 110, 14), q.ProgressFraction(w), "ui_fill_green");
                r.Text(q.ProgressText(w), Fonts.Small, Theme.Light, x + width - 86, y + 74);
            }

            var debt = w.Debt;
            int due = debt.DueThisSeason;
            string dueText = debt.IsPaidOff ? "Utang lunas!"
                : due > 0 ? $"Cicilan kurang {due} - {w.Clock.DaysUntilNextSeason} hari lagi" : "Cicilan musim ini aman";
            bool urgent = due > 0 && w.Clock.DaysUntilNextSeason <= 3;
            r.Draw("ui_icon_debt", x + 16, y + 92);
            r.Text(dueText, Fonts.Small, urgent ? Theme.Warn : Theme.Light, x + 40, y + 93);
        }

        /// <summary>Hotbar alat. Mengembalikan posisi Y teratas hotbar (untuk menaruh petunjuk aksi).</summary>
        private static int DrawHotbar(Renderer r, int vw, int vh, FarmWorld w)
        {
            var p = w.Player;
            var tools = p.Tools;
            int gap = 4, width = tools.Count * (SlotSize + gap) - gap;
            int x0 = vw / 2 - width / 2, y0 = vh - SlotSize - 34;
            r.DrawFrame("ui_frame", x0 - 12, y0 - 12, width + 24, SlotSize + 24);

            for (int i = 0; i < tools.Count; i++)
            {
                var t = tools[i];
                int x = x0 + i * (SlotSize + gap);
                bool selected = i == p.ActiveSlot;
                r.Draw(selected ? "ui_slot_selected" : "ui_slot", x, y0);
                r.DrawCentered(Art.Get(t.IconName), x + SlotSize / 2f, y0 + SlotSize / 2f + (selected ? -2 : 0));
                r.Text((i + 1).ToString(), Fonts.Small, Theme.InkSoft, x + 5, y0 + 3, shadow: false);
                if (t.Badge(p) is { } badge)
                {
                    float bw = r.Measure(badge, Fonts.Body).Width;
                    r.Text(badge, Fonts.Body, Color.White, x + SlotSize - bw - 5, y0 + SlotSize - 19);
                }
            }
            string name = p.ActiveTool.Name;
            r.TextCentered(name, Fonts.Body, Color.White, vw / 2f, y0 + SlotSize + 14);
            return y0 - 12;
        }

        private static void DrawBars(Renderer r, int vw, int vh, FarmWorld w)
        {
            var p = w.Player;
            int barH = 150, y = vh - barH - 64;
            r.DrawBar(new Rectangle(vw - 46, y, 30, barH), p.EnergyFraction, p.EnergyFraction < 0.2f ? "ui_fill_red" : "ui_fill_green", vertical: true);
            r.DrawCentered(Art.Get("ui_icon_energy"), vw - 31, y + barH + 16);
            r.DrawBar(new Rectangle(vw - 86, y, 30, barH), p.Can.Water / (float)p.Can.Capacity, "ui_fill_blue", vertical: true);
            r.DrawCentered(Art.Get("ui_icon_water"), vw - 71, y + barH + 16);
        }

        private static void DrawPrompt(Renderer r, int vw, int hotbarTop, FarmWorld w)
        {
            if (w.FindInteractable() is not { } target) return;
            string text = target.Prompt(w);
            float textW = r.Measure(text, Fonts.Large).Width;
            int width = (int)textW + 70, x = vw / 2 - width / 2, y = hotbarTop - 52;
            r.DrawFrame("ui_frame", x, y, width, 44);
            var key = Art.Get("ui_key");
            r.Draw(key, x + 12, y + 11);
            r.TextCentered("E", Fonts.Body, Theme.Ink, x + 12 + key.Width / 2f, y + 13, shadow: false);
            r.Text(text, Fonts.Large, Theme.Ink, x + 46, y + 12, shadow: false);
        }

        private static void DrawMessage(Renderer r, int vw, FarmWorld w)
        {
            if (w.MessageTimer <= 0 || w.Message.Length == 0) return;
            float textW = r.Measure(w.Message, Fonts.Body).Width;
            int width = (int)textW + 40, x = vw / 2 - width / 2;
            r.DrawFrame("ui_frame_dark", x, 140, width, 40);
            r.Text(w.Message, Fonts.Body, Theme.Light, x + 20, 151, alpha: Math.Min(1f, w.MessageTimer * 2));
        }
    }
}
