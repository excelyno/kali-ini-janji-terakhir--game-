using System.Collections.Generic;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Economy;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Entities.Npcs
{
    // PEWARISAN: ketiga warga mewarisi Npc (yang mewarisi Character → Entity → GameObject).
    // POLIMORFISME: masing-masing mengisi CreateDialogue dengan percakapannya sendiri.

    /// <summary>Kakek Wira: tetangga tua. Pertemuan pertama memberi benih, setelahnya memberi nasihat musim.</summary>
    public sealed class Wira : Npc
    {
        private const string GiftFlag = "wira_gift";
        private int tipIndex;

        public Wira(IEnumerable<PointF> route) : base("Kakek Wira", route, "wira") { }

        public override string Role => "Tetangga";

        protected override bool HasNews(FarmWorld world) => base.HasNews(world) || !world.HasFlag(GiftFlag);

        protected override Dialogue CreateDialogue(FarmWorld world)
        {
            if (!world.HasFlag(GiftFlag))
            {
                return new Dialogue(Name,
                        "Oh, kamu cucunya Sekar? Selamat datang di ladang ini, Nak.",
                        "Nenekmu meninggalkan ladang... dan juga utang 5.000 koin pada Pak Rentenir.",
                        "Jangan khawatir. Tanah ini subur. Ambil benih ini dan mulailah mencangkul di kebun berpagar.")
                    .Choice("Terima kasih, Kek!", w =>
                    {
                        w.SetFlag(GiftFlag);
                        w.Player.Inventory.Add(ItemDb.Seed(CropKind.Wortel), 5);
                        w.Player.Inventory.Add(ItemDb.Seed(CropKind.Lobak), 3);
                        w.Say("Dapat 5 Benih Wortel dan 3 Benih Lobak!");
                    });
            }

            var season = world.Clock.Season;
            string[] tips =
            {
                season == Season.Dingin
                    ? "Musim Dingin, hanya Lobak yang mau tumbuh. Tanaman lain diam menunggu musim semi."
                    : $"Musim {GameClock.SeasonName(season)}. Cek di toko tanaman apa yang cocok musim ini.",
                "Siram sebelum tidur. Tanaman hanya tumbuh di malam hari kalau tanahnya basah.",
                "Kalau hujan, kamu tak perlu menyiram. Langit yang mengerjakannya.",
                "Tomat itu sabar: sekali tumbuh, dipetik berkali-kali.",
                "Hasil panen yang langka di musimnya dijual lebih mahal. Itu hukum pasar, Nak.",
            };
            string tip = tips[tipIndex++ % tips.Length];
            return new Dialogue(Name, tip);
        }
    }

    /// <summary>Bu Lastri: pemilik toko. Bicara dengannya bisa langsung membuka toko.</summary>
    public sealed class Lastri : Npc
    {
        public Lastri(IEnumerable<PointF> route) : base("Bu Lastri", route, "lastri") { }

        public override string Role => "Pemilik toko";

        protected override Dialogue CreateDialogue(FarmWorld world)
        {
            string chat = world.Weather == WeatherKind.Hujan
                ? "Hujan begini, pembeli sepi. Tapi kebunmu pasti senang."
                : "Benih segar, harga bersahabat! Jangan lupa hitung modal dan untungnya ya.";
            return new Dialogue(Name, chat)
                .Choice("Lihat dagangan", w => w.OpenShop())
                .Choice("Nanti saja", _ => { });
        }
    }

    /// <summary>
    /// Pak Rentenir: penagih utang. Menampilkan sisa utang dan pilihan cicilan.
    /// Semua perubahan angka utang lewat method Debt.Pay (ENKAPSULASI).
    /// </summary>
    public sealed class Rentenir : Npc
    {
        public Rentenir(IEnumerable<PointF> route) : base("Pak Rentenir", route, "rentenir") { }

        public override string Role => "Penagih utang";

        protected override bool HasNews(FarmWorld world)
            => base.HasNews(world) || (world.Debt.DueThisSeason > 0 && world.Clock.DaysUntilNextSeason <= 3);

        protected override Dialogue CreateDialogue(FarmWorld world)
        {
            var debt = world.Debt;
            if (debt.IsPaidOff)
                return new Dialogue(Name, "Utangmu sudah lunas. Hmph... nenekmu pasti bangga.");

            string status = $"Sisa utang: {debt.Remaining} koin. Bunga {debt.RatePercent}% per musim" +
                            (debt.InGrace ? " (musim ini masih masa tenggang)." : ".");
            string due = debt.DueThisSeason > 0
                ? $"Cicilan musim ini kurang {debt.DueThisSeason} koin. Aku datang lagi {world.Clock.DaysUntilNextSeason} hari lagi."
                : "Cicilan musim ini sudah cukup. Membayar lebih awal mengurangi bunga.";

            var d = new Dialogue(Name, status, due);
            if (debt.DueThisSeason > 0) d.Choice($"Bayar cicilan ({debt.DueThisSeason})", w => PayDebt(w, w.Debt.DueThisSeason));
            d.Choice("Bayar 500", w => PayDebt(w, 500));
            d.Choice("Bayar semampunya", w => PayDebt(w, w.Player.Wallet.Coins));
            d.Choice("Nanti saja", _ => { });
            return d;
        }

        private static void PayDebt(FarmWorld world, int amount)
        {
            int paid = world.Debt.Pay(world.Player.Wallet, amount);
            if (paid <= 0)
            {
                world.Say("Koinmu tidak cukup.");
                return;
            }
            world.Say($"Membayar {paid} koin. Sisa utang {world.Debt.Remaining}.");
            world.Raise(GameEventType.DebtPaid, paid);
            if (world.Debt.IsPaidOff) world.TriggerEnding();
        }
    }
}
