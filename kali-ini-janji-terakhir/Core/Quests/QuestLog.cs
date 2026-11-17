using System.Collections.Generic;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Economy;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Quests
{
    /// <summary>
    /// Rantai misi utama. Seperti kisah utama TANDUR, hanya satu misi aktif dalam satu waktu.
    /// ENKAPSULASI: daftar misi dan indeks aktif privat; luar kelas hanya bisa membaca.
    /// </summary>
    public sealed class QuestLog
    {
        private readonly List<Quest> quests = new()
        {
            new TalkQuest("Kakek Wira", "Tetangga Lama", "Sapa Kakek Wira di dekat kebun.", "Kakek Wira",
                new Reward(0), "Bertanya pada petani yang lebih tua adalah cara tercepat belajar."),
            new CountQuest("Kakek Wira", "Tanah yang Gembur", "Cangkul 6 petak di kebun berpagar.", GameEventType.Tilled, 6,
                new Reward(30), "Tanah yang digemburkan membuat akar mudah menembus dan air meresap merata."),
            new CountQuest("Kakek Wira", "Benih Pertama", "Tanam 6 benih di petak yang sudah dicangkul.", GameEventType.Planted, 6,
                new Reward(20, ItemDb.Seed(CropKind.Lobak), 4), "Satu benih kecil menyimpan seluruh tanaman."),
            new CountQuest("Kakek Wira", "Air Kehidupan", "Siram petak 6 kali. Tanaman tumbuh saat kamu tidur jika sudah disiram.", GameEventType.Watered, 6,
                new Reward(25), "Air mengangkut makanan dari tanah ke seluruh tanaman."),
            new CountQuest("Bu Lastri", "Panen Pertama", "Panen 4 hasil apa saja.", GameEventType.Harvested, 4,
                new Reward(60), "Panen tepat waktu menjaga mutu. Tomat bisa dipanen berulang."),
            new CountQuest("Bu Lastri", "Untung = Harga - Modal", "Dapatkan 300 koin dari peti jual.", GameEventType.Earned, 300,
                new Reward(50), "Untung adalah harga jual dikurangi modal benih. Hitung sebelum menanam!"),
            new TalkQuest("Pak Rentenir", "Si Penagih", "Temui Pak Rentenir di jalan timur.", "Pak Rentenir",
                new Reward(0), "Utang punya bunga. Makin lama dibayar, makin besar jumlahnya."),
            new DebtQuest("Pak Rentenir", "Cicilan Pertama", "Bayar cicilan minimal 300 koin ke Pak Rentenir.", 300,
                new Reward(0, ItemDb.Seed(CropKind.Jagung), 3), "Membayar tepat waktu menghindari denda dan bunga naik."),
            new CountQuest("Kakek Wira", "Kayu Bakar", "Tebang 3 pohon dengan kapak.", GameEventType.Chopped, 3,
                new Reward(40), "Ambil kayu secukupnya. Hutan yang lestari memberi hasil selamanya."),
            new CountQuest("Bu Lastri", "Telur Segar", "Ambil 3 telur dari ayam (E di dekat ayam).", GameEventType.EggCollected, 3,
                new Reward(45), "Ternak memberi penghasilan harian, tidak menunggu musim panen."),
            new DebtQuest("Pak Rentenir", "Separuh Jalan", "Bayar total 2500 koin utang.", Debt.Initial / 2,
                new Reward(100), "Menabung sedikit demi sedikit lebih ringan daripada membayar sekaligus."),
            new DebtQuest("Pak Rentenir", "Bebas Utang", "Lunasi seluruh utang warisan.", int.MaxValue,
                new Reward(0), "Bebas utang berarti seluruh hasil panen menjadi milikmu sendiri."),
        };

        private int activeIndex;
        private readonly List<string> lessons = new();

        public Quest? Active => activeIndex < quests.Count ? quests[activeIndex] : null;
        public int ActiveIndex => activeIndex;
        public int TotalCount => quests.Count;
        public IReadOnlyList<string> Lessons => lessons;

        /// <summary>Teruskan event ke misi aktif. Mengembalikan misi yang baru selesai (jika ada).</summary>
        public Quest? Handle(GameEvent e, FarmWorld world)
        {
            var q = Active;
            if (q == null) return null;
            q.Handle(e);
            return TryComplete(world);
        }

        /// <summary>Cek misi yang selesai tanpa event (misal misi utang).</summary>
        public Quest? TryComplete(FarmWorld world)
        {
            var q = Active;
            if (q == null || !q.IsComplete(world)) return null;
            lessons.Add(q.Title + ": " + q.Lesson);
            activeIndex++;
            return q;
        }

        public void Restore(int index, int progress)
        {
            activeIndex = System.Math.Clamp(index, 0, quests.Count);
            lessons.Clear();
            for (int i = 0; i < activeIndex; i++) lessons.Add(quests[i].Title + ": " + quests[i].Lesson);
            Active?.RestoreProgress(progress);
        }
    }
}
