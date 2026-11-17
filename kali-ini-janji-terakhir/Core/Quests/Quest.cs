using System;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core.Quests
{
    /// <summary>Hadiah misi: koin dan/atau barang.</summary>
    public sealed record Reward(int Coins, Item? Item = null, int ItemAmount = 0)
    {
        public override string ToString()
        {
            string s = Coins > 0 ? Coins + " koin" : "";
            if (Item != null) s += (s.Length > 0 ? " + " : "") + ItemAmount + " " + Item.Name;
            return s.Length > 0 ? s : "-";
        }
    }

    /// <summary>
    /// ABSTRAKSI: misi. QuestLog hanya tahu "misi bisa menerima event, punya progres,
    /// dan bisa selesai". Cara menghitung progres diserahkan ke subclass.
    /// Setiap misi membuka satu pelajaran bertani/keuangan (ide dari TANDUR).
    /// </summary>
    public abstract class Quest
    {
        protected Quest(string giver, string title, string description, Reward reward, string lesson)
        {
            Giver = giver;
            Title = title;
            Description = description;
            Reward = reward;
            Lesson = lesson;
        }

        public string Giver { get; }
        public string Title { get; }
        public string Description { get; }
        public Reward Reward { get; }
        public string Lesson { get; }

        public abstract bool IsComplete(FarmWorld world);

        /// <summary>Teks progres singkat, misalnya "3/6".</summary>
        public abstract string ProgressText(FarmWorld world);

        /// <summary>Progres 0..1 untuk bar.</summary>
        public abstract float ProgressFraction(FarmWorld world);

        public abstract void Handle(GameEvent e);

        /// <summary>Progres yang perlu disimpan (default tidak ada).</summary>
        public virtual int SavedProgress => 0;
        public virtual void RestoreProgress(int value) { }
    }

    /// <summary>
    /// Misi menghitung event: "cangkul 6 petak", "panen 5", "dapat 300 koin".
    /// ENKAPSULASI: hitungan privat, hanya bertambah lewat Handle().
    /// </summary>
    public class CountQuest : Quest
    {
        private int count;

        public CountQuest(string giver, string title, string description, GameEventType type, int target,
                          Reward reward, string lesson, string? subject = null)
            : base(giver, title, description, reward, lesson)
        {
            Type = type;
            Target = target;
            Subject = subject;
        }

        public GameEventType Type { get; }
        public int Target { get; }
        public string? Subject { get; }
        public int Count => count;

        public override void Handle(GameEvent e)
        {
            if (e.Type != Type) return;
            if (Subject != null && !string.Equals(Subject, e.Subject, StringComparison.Ordinal)) return;
            count = Math.Min(Target, count + e.Amount);
        }

        public override bool IsComplete(FarmWorld world) => count >= Target;
        public override string ProgressText(FarmWorld world) => $"{count}/{Target}";
        public override float ProgressFraction(FarmWorld world) => count / (float)Target;

        public override int SavedProgress => count;
        public override void RestoreProgress(int value) => count = Math.Clamp(value, 0, Target);
    }

    /// <summary>Misi bicara dengan seseorang. PEWARISAN: CountQuest dengan target 1 dan subjek nama warga.</summary>
    public sealed class TalkQuest : CountQuest
    {
        public TalkQuest(string giver, string title, string description, string npcName, Reward reward, string lesson)
            : base(giver, title, description, GameEventType.Talked, 1, reward, lesson, npcName) { }

        public override string ProgressText(FarmWorld world) => Count > 0 ? "selesai" : "belum";
    }

    /// <summary>Misi utang: progres dibaca langsung dari objek Debt, bukan dari hitungan event.</summary>
    public sealed class DebtQuest : Quest
    {
        private readonly int targetPaid;

        public DebtQuest(string giver, string title, string description, int targetPaid, Reward reward, string lesson)
            : base(giver, title, description, reward, lesson) => this.targetPaid = targetPaid;

        public override void Handle(GameEvent e) { }

        public override bool IsComplete(FarmWorld world) => world.Debt.PaidTotal >= targetPaid || world.Debt.IsPaidOff;
        private bool IsFullPayoff => targetPaid == int.MaxValue;

        public override string ProgressText(FarmWorld world)
            => IsFullPayoff ? $"sisa {world.Debt.Remaining}" : $"{Math.Min(world.Debt.PaidTotal, targetPaid)}/{targetPaid}";

        public override float ProgressFraction(FarmWorld world)
        {
            var d = world.Debt;
            float target = IsFullPayoff ? d.PaidTotal + d.Remaining : targetPaid;
            return Math.Min(1f, d.PaidTotal / Math.Max(1f, target));
        }
    }
}
