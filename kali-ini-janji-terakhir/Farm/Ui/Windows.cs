using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using kali_ini_janji_terakhir.Core;
using kali_ini_janji_terakhir.Core.Economy;
using kali_ini_janji_terakhir.Core.Entities.Npcs;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Farm.Ui
{
    /// <summary>Kotak percakapan: teks muncul huruf demi huruf, potret warga di kanan, lalu pilihan.</summary>
    public sealed class DialogWindow : UiWindow
    {
        private readonly Dialogue dialogue;
        private readonly Npc? speaker;
        private int page;
        private float shown;
        private int choice;
        private float time;

        public DialogWindow(Dialogue dialogue, Npc? speaker = null)
        {
            this.dialogue = dialogue;
            this.speaker = speaker;
        }

        private string Text => dialogue.Pages.Count > 0 ? dialogue.Pages[page] : "";
        private bool Typing => shown < Text.Length;
        private bool OnLastPage => page >= dialogue.Pages.Count - 1;
        private bool ShowingChoices => OnLastPage && !Typing && dialogue.Choices.Count > 0;

        public override void Update(float dt)
        {
            time += dt;
            shown = Math.Min(Text.Length, shown + dt * 55);
        }

        public override void HandleKey(Keys key, FarmWorld world)
        {
            if (ShowingChoices)
            {
                int n = dialogue.Choices.Count;
                if (IsUp(key)) choice = (choice + n - 1) % n;
                else if (IsDown(key)) choice = (choice + 1) % n;
                else if (IsConfirm(key))
                {
                    Close();
                    dialogue.Choices[choice].Effect(world);
                }
                return;
            }
            if (!IsConfirm(key) && !IsBack(key)) return;
            if (Typing) { shown = Text.Length; return; }
            if (!OnLastPage) { page++; shown = 0; return; }
            Close();
        }

        public override void Draw(Renderer r, int vw, int vh, FarmWorld world)
        {
            int portraitW = speaker != null ? 190 : 0;
            int w = Math.Min(980, vw - 40), h = 200, x = vw / 2 - w / 2, y = vh - h - 20;
            int textW = w - portraitW - 12;
            r.DrawFrame("ui_frame", x, y, textW, h);

            var lines = r.Wrap(Text.Substring(0, (int)shown), Fonts.Large, textW - 56);
            for (int i = 0; i < lines.Count; i++) r.Text(lines[i], Fonts.Large, Theme.Ink, x + 28, y + 26 + i * 28, shadow: false);

            if (speaker != null)
            {
                int px = x + textW + 12;
                r.DrawFrame("ui_frame", px, y, portraitW, h);
                var face = speaker.Portrait;
                float scale = 1.5f;
                r.DrawStretched(face, new RectangleF(px + portraitW / 2f - face.Width * scale / 2, y + 12, face.Width * scale, face.Height * scale));
                r.TextCentered(speaker.Name, Fonts.Body, Theme.Ink, px + portraitW / 2f, y + h - 44, shadow: false);
                r.TextCentered(speaker.Role, Fonts.Small, Theme.InkSoft, px + portraitW / 2f, y + h - 24, shadow: false);
            }
            else
            {
                float nameW = r.Measure(dialogue.Speaker, Fonts.Body).Width;
                r.DrawFrame("ui_frame_dark", x + 16, y - 30, (int)nameW + 32, 38);
                r.Text(dialogue.Speaker, Fonts.Body, Theme.Gold, x + 32, y - 20);
            }

            if (ShowingChoices) DrawChoices(r, x + textW, y);
            else if (!Typing) r.Draw("ui_arrow_down", x + textW - 34, y + h - 30 + MathF.Sin(time * 6) * 3);
        }

        private void DrawChoices(Renderer r, int right, int bottom)
        {
            int n = dialogue.Choices.Count, cw = 320, ch = n * 36 + 28;
            int cx = right - cw, cy = bottom - ch - 8;
            r.DrawFrame("ui_frame", cx, cy, cw, ch);
            for (int i = 0; i < n; i++)
            {
                int yy = cy + 14 + i * 36;
                if (i == choice) r.Draw("ui_arrow_right", cx + 16, yy + 8);
                r.Text(dialogue.Choices[i].Label, Fonts.Large, i == choice ? Theme.Ink : Theme.InkSoft, cx + 36, yy + 4, shadow: false);
            }
        }
    }

    /// <summary>Toko Bu Lastri. Setiap baris adalah ShopOffer; pembelian lewat offer.TryBuy (polimorfisme).</summary>
    public sealed class ShopWindow : MenuWindow
    {
        private readonly FarmWorld world;

        public ShopWindow(FarmWorld world) => this.world = world;

        protected override int OptionCount => world.ShopOffers.Count + 1;

        protected override void Choose(int index, FarmWorld w)
        {
            if (index >= w.ShopOffers.Count) Close();
            else w.ShopOffers[index].TryBuy(w);
        }

        public override void Draw(Renderer r, int vw, int vh, FarmWorld w)
        {
            r.Tint(Theme.Dim, new Rectangle(0, 0, vw, vh));
            int rows = OptionCount, rowH = 54, width = Math.Min(760, vw - 40), h = 110 + rows * rowH;
            int x = vw / 2 - width / 2, y = Math.Max(10, vh / 2 - h / 2);
            r.DrawFrame("ui_frame", x, y, width, h);
            r.Text("Toko Bu Lastri", Fonts.Large, Theme.Ink, x + 26, y + 22, shadow: false);
            r.Draw("ui_icon_coin", x + width - 150, y + 24);
            r.Text(w.Player.Wallet.Coins.ToString("N0"), Fonts.Large, Theme.Ink, x + width - 124, y + 22, shadow: false);

            for (int i = 0; i < rows; i++)
            {
                int yy = y + 58 + i * rowH;
                if (i == Selected) Theme.Highlight(r, new Rectangle(x + 16, yy - 2, width - 32, rowH - 4));
                if (i == w.ShopOffers.Count)
                {
                    r.Text("Tutup", Fonts.Large, Theme.Ink, x + 70, yy + 14, shadow: false);
                    continue;
                }
                ShopOffer o = w.ShopOffers[i];
                string? reason = o.Unavailable(w);
                r.DrawCentered(Art.Get(o.IconName), x + 46, yy + rowH / 2f - 2);
                r.Text(o.Name, Fonts.Large, Theme.Ink, x + 76, yy + 4, shadow: false);
                r.Text(o.Description(w), Fonts.Small, Theme.InkSoft, x + 76, yy + 28, shadow: false);
                string price = reason ?? o.Price + " koin";
                float priceW = r.Measure(price, Fonts.Large).Width;
                r.Text(price, Fonts.Large, reason != null ? Theme.InkSoft : Theme.Ink, x + width - 32 - priceW, yy + 14, shadow: false);
            }
            r.Text("Atas/Bawah pilih  |  Enter beli  |  Esc tutup", Fonts.Small, Theme.InkSoft, x + 26, y + h - 34, shadow: false);
        }
    }

    /// <summary>Jurnal (Q): misi aktif, utang, isi tas (ikon), dan catatan pelajaran dari misi selesai.</summary>
    public sealed class JournalWindow : UiWindow
    {
        public override void HandleKey(Keys key, FarmWorld world)
        {
            if (IsBack(key) || IsConfirm(key) || key is Keys.Q or Keys.Tab) Close();
        }

        public override void Draw(Renderer r, int vw, int vh, FarmWorld w)
        {
            r.Tint(Theme.Dim, new Rectangle(0, 0, vw, vh));
            int width = Math.Min(1000, vw - 30), h = Math.Min(580, vh - 30), x = vw / 2 - width / 2, y = vh / 2 - h / 2;
            r.DrawFrame("ui_frame", x, y, width, h);
            r.Draw("ui_backpack", x + 24, y + 18);
            r.Text("Jurnal Petani", Fonts.Large, Theme.Ink, x + 64, y + 24, shadow: false);
            int col = width / 2;

            // kiri: misi + utang + tas
            float ly = y + 70;
            var q = w.Quests.Active;
            r.Text($"Misi ({Math.Min(w.Quests.ActiveIndex + 1, w.Quests.TotalCount)}/{w.Quests.TotalCount})", Fonts.Body, Theme.InkSoft, x + 28, ly, shadow: false);
            ly += 22;
            if (q != null)
            {
                r.Text(q.Title + "  - dari " + q.Giver, Fonts.Large, Theme.Ink, x + 28, ly, shadow: false); ly += 26;
                foreach (var line in r.Wrap(q.Description, Fonts.Body, col - 50)) { r.Text(line, Fonts.Body, Theme.InkSoft, x + 28, ly, shadow: false); ly += 19; }
                r.DrawBar(new Rectangle(x + 28, (int)ly + 4, col - 130, 14), q.ProgressFraction(w), "ui_fill_green");
                r.Text(q.ProgressText(w), Fonts.Body, Theme.Ink, x + col - 92, ly + 3, shadow: false);
                ly += 26;
                r.Text("Hadiah: " + q.Reward, Fonts.Body, Theme.Ink, x + 28, ly, shadow: false); ly += 32;
            }
            else { r.Text("Semua misi selesai!", Fonts.Large, Theme.Ink, x + 28, ly, shadow: false); ly += 34; }

            var d = w.Debt;
            r.Text("Utang ke Pak Rentenir", Fonts.Body, Theme.InkSoft, x + 28, ly, shadow: false); ly += 22;
            r.Text($"Sisa {d.Remaining} koin  |  terbayar {d.PaidTotal}", Fonts.Body, Theme.Ink, x + 28, ly, shadow: false); ly += 22;
            r.DrawBar(new Rectangle(x + 28, (int)ly, col - 56, 14), d.PaidFraction, "ui_fill_gold"); ly += 20;
            r.Text($"Bunga {d.RatePercent}%/musim{(d.InGrace ? " (masa tenggang)" : "")}  |  menunggak {d.MissedSeasons}x", Fonts.Small, Theme.InkSoft, x + 28, ly, shadow: false); ly += 30;

            r.Text("Tas", Fonts.Body, Theme.InkSoft, x + 28, ly, shadow: false); ly += 22;
            var entries = w.Player.Inventory.Entries;
            if (entries.Count == 0) r.Text("(kosong)", Fonts.Body, Theme.InkSoft, x + 28, ly, shadow: false);
            for (int i = 0; i < entries.Count; i++)
            {
                var (item, n) = entries[i];
                int sx = x + 28 + i % 7 * 60, sy = (int)ly + i / 7 * 60;
                if (sy + Hud.SlotSize > y + h - 20) break;
                r.Draw("ui_slot", sx, sy);
                r.DrawCentered(Art.Get(item.IconName), sx + Hud.SlotSize / 2f, sy + Hud.SlotSize / 2f);
                string count = n.ToString();
                r.Text(count, Fonts.Body, Color.White, sx + Hud.SlotSize - 6 - r.Measure(count, Fonts.Body).Width, sy + Hud.SlotSize - 20);
            }

            // kanan: catatan tani
            float ry = y + 70;
            r.Text("Catatan Tani", Fonts.Body, Theme.InkSoft, x + col + 20, ry, shadow: false); ry += 26;
            if (w.Quests.Lessons.Count == 0) r.Text("Selesaikan misi untuk membuka catatan.", Fonts.Body, Theme.InkSoft, x + col + 20, ry, shadow: false);
            foreach (var lesson in w.Quests.Lessons.Reverse().Take(8))
            {
                foreach (var line in r.Wrap("- " + lesson, Fonts.Body, col - 60))
                {
                    if (ry > y + h - 50) break;
                    r.Text(line, Fonts.Body, Theme.Ink, x + col + 20, ry, shadow: false);
                    ry += 19;
                }
                ry += 8;
            }
            r.Text("Q / Esc tutup", Fonts.Small, Theme.InkSoft, x + width - 120, y + h - 34, shadow: false);
        }
    }

    /// <summary>Menu jeda: lanjut, simpan, atau simpan lalu ke menu utama.</summary>
    public sealed class PauseWindow : MenuWindow
    {
        private static readonly string[] Options = { "Lanjutkan", "Simpan", "Simpan & ke menu utama" };
        private readonly Action save;
        private readonly Action exit;

        public PauseWindow(Action save, Action exit)
        {
            this.save = save;
            this.exit = exit;
        }

        protected override int OptionCount => Options.Length;

        public override void HandleKey(Keys key, FarmWorld world)
        {
            if (key == Keys.P) { Close(); return; }
            base.HandleKey(key, world);
        }

        protected override void Choose(int index, FarmWorld world)
        {
            switch (index)
            {
                case 0: Close(); break;
                case 1: save(); world.Say("Permainan disimpan."); Close(); break;
                default: save(); exit(); break;
            }
        }

        public override void Draw(Renderer r, int vw, int vh, FarmWorld world)
        {
            r.Tint(Theme.Dim, new Rectangle(0, 0, vw, vh));
            int w = 380, h = 90 + Options.Length * 48, x = vw / 2 - w / 2, y = vh / 2 - h / 2;
            r.DrawFrame("ui_frame", x, y, w, h);
            r.TextCentered("Jeda", Fonts.Large, Theme.Ink, vw / 2f, y + 22, shadow: false);
            for (int i = 0; i < Options.Length; i++)
            {
                int yy = y + 62 + i * 48;
                if (i == Selected) Theme.Highlight(r, new Rectangle(x + 20, yy, w - 40, 42));
                r.TextCentered(Options[i], Fonts.Large, Theme.Ink, vw / 2f, yy + 11, shadow: false);
            }
        }
    }

    /// <summary>Akhir permainan: Festival Panen setelah utang lunas.</summary>
    public sealed class EndingWindow : UiWindow
    {
        private readonly Action restart;
        private readonly Action exit;
        private float t;

        public EndingWindow(Action restart, Action exit)
        {
            this.restart = restart;
            this.exit = exit;
        }

        public override void Update(float dt) => t += dt;

        public override void HandleKey(Keys key, FarmWorld world)
        {
            if (t < 1f) return;
            if (key is Keys.R or Keys.Enter) restart();
            else if (key is Keys.Escape or Keys.M) exit();
        }

        public override void Click(FarmWorld world) { }

        public override void Draw(Renderer r, int vw, int vh, FarmWorld w)
        {
            r.Tint(Color.FromArgb((int)(Math.Min(1f, t) * 200), 10, 5, 20), new Rectangle(0, 0, vw, vh));
            var rnd = new Random(3);
            for (int i = 0; i < 90; i++)   // konfeti festival (sprite)
            {
                float x = (float)rnd.NextDouble() * vw, speed = 40 + (float)rnd.NextDouble() * 80;
                float y = ((float)rnd.NextDouble() * vh + t * speed) % vh;
                r.Draw($"fx_confetti_{i % 4}", x + MathF.Sin(t * 3 + i) * 8, y);
            }

            int bw = Math.Min(720, vw - 40), bh = 300, bx = vw / 2 - bw / 2, by = vh / 2 - bh / 2;
            r.DrawFrame("ui_frame", bx, by, bw, bh);
            float cx = vw / 2f;
            r.TextCentered("FESTIVAL PANEN", Fonts.Title, Theme.Ink, cx, by + 26, shadow: false);
            r.TextCentered("Utang warisan nenek Sekar lunas!", Fonts.Large, Theme.Ink, cx, by + 90, shadow: false);
            r.TextCentered($"Lunas dalam {w.Clock.Day} hari (tahun {w.Clock.Year}, musim {GameClock.SeasonName(w.Clock.Season)}).", Fonts.Body, Theme.InkSoft, cx, by + 126, shadow: false);
            r.TextCentered($"Total pendapatan {w.Player.Wallet.TotalEarned} koin, menunggak {w.Debt.MissedSeasons}x.", Fonts.Body, Theme.InkSoft, cx, by + 150, shadow: false);
            string title = w.Debt.MissedSeasons == 0 && w.Clock.SeasonIndex < 6 ? "Gelar: Si Teliti"
                         : w.Debt.MissedSeasons >= 2 ? "Gelar: Petani yang Gigih" : "Gelar: Tangan yang Tekun";
            r.TextCentered(title, Fonts.Large, Theme.Ink, cx, by + 186, shadow: false);
            if (t > 1f) r.TextCentered("R main lagi  |  Esc / M ke menu utama", Fonts.Body, Theme.InkSoft, cx, by + bh - 50, shadow: false);
        }
    }
}
