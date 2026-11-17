using System;

namespace kali_ini_janji_terakhir.Core.Economy
{
    public sealed record DebtSave(int Remaining, int PaidTotal, int PaidThisSeason, int RatePermille, int Missed, int GraceSeasons);

    /// <summary>Laporan saat Pak Rentenir datang di awal musim.</summary>
    public readonly record struct DebtReport(bool WasGrace, bool Missed, int Interest, int LateFee, int RatePercent, int Remaining);

    /// <summary>
    /// Utang warisan kepada Pak Rentenir (aturan diadaptasi dari TANDUR):
    /// - cicilan minimal 300 per musim;
    /// - musim pertama masa tenggang: tanpa bunga dan denda;
    /// - setelahnya bunga 5% per musim dari sisa utang;
    /// - menunggak: denda 10% dari kekurangan cicilan, bunga naik 1% (maks 9%).
    /// ENKAPSULASI: semua angka privat dan hanya berubah lewat Pay() dan EndSeason().
    /// </summary>
    public sealed class Debt
    {
        public const int Initial = 5000;
        public const int Installment = 300;
        private const int BaseRatePermille = 50;
        private const int RateStepPermille = 10;
        private const int MaxRatePermille = 90;

        private int remaining = Initial;
        private int paidTotal;
        private int paidThisSeason;
        private int ratePermille = BaseRatePermille;
        private int missed;
        private int graceSeasons = 1;

        public int Remaining => remaining;
        public int PaidTotal => paidTotal;
        public int PaidThisSeason => paidThisSeason;
        public int RatePercent => ratePermille / 10;
        public int MissedSeasons => missed;
        public bool IsPaidOff => remaining <= 0;
        public bool InGrace => graceSeasons > 0;
        public float PaidFraction => Math.Clamp(paidTotal / (float)Initial, 0f, 1f);

        /// <summary>Sisa cicilan musim ini agar tidak kena denda.</summary>
        public int DueThisSeason => Math.Max(0, Math.Min(Installment, remaining) - paidThisSeason);

        /// <summary>Bayar dari dompet. Mengembalikan jumlah yang benar-benar terbayar.</summary>
        public int Pay(Wallet wallet, int amount)
        {
            int paid = Math.Max(0, Math.Min(amount, Math.Min(wallet.Coins, remaining)));
            if (paid == 0 || !wallet.TrySpend(paid)) return 0;
            remaining -= paid;
            paidTotal += paid;
            paidThisSeason += paid;
            return paid;
        }

        /// <summary>Dipanggil saat musim berganti.</summary>
        public DebtReport EndSeason()
        {
            if (IsPaidOff) return new DebtReport(false, false, 0, 0, RatePercent, 0);

            bool grace = graceSeasons > 0;
            int due = DueThisSeason;
            bool late = due > 0 && !grace;
            int fee = 0, interest = 0;

            if (grace)
            {
                graceSeasons--;
            }
            else
            {
                if (late)
                {
                    fee = (int)Math.Ceiling(due * 0.10);
                    missed++;
                    ratePermille = Math.Min(MaxRatePermille, ratePermille + RateStepPermille);
                }
                else
                {
                    missed = 0;
                }
                remaining += fee;
                interest = (int)Math.Ceiling(remaining * ratePermille / 1000.0);
                remaining += interest;
            }

            paidThisSeason = 0;
            return new DebtReport(grace, late, interest, fee, RatePercent, remaining);
        }

        public DebtSave ToSave() => new(remaining, paidTotal, paidThisSeason, ratePermille, missed, graceSeasons);

        public void Restore(DebtSave? s)
        {
            if (s == null) return;
            remaining = Math.Max(0, s.Remaining);
            paidTotal = Math.Max(0, s.PaidTotal);
            paidThisSeason = Math.Max(0, s.PaidThisSeason);
            ratePermille = Math.Clamp(s.RatePermille, BaseRatePermille, MaxRatePermille);
            missed = Math.Max(0, s.Missed);
            graceSeasons = Math.Max(0, s.GraceSeasons);
        }
    }
}
