using System;

namespace kali_ini_janji_terakhir.Core.Economy
{
    /// <summary>
    /// Dompet koin. ENKAPSULASI: saldo privat, tidak ada setter publik.
    /// Uang hanya masuk lewat Earn dan keluar lewat TrySpend yang menolak bila saldo kurang.
    /// </summary>
    public sealed class Wallet
    {
        private int coins;

        public Wallet(int startingCoins = 0)
        {
            if (startingCoins < 0) throw new ArgumentOutOfRangeException(nameof(startingCoins));
            coins = startingCoins;
        }

        public int Coins => coins;
        public int TotalEarned { get; private set; }

        public void Earn(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            coins += amount;
            TotalEarned += amount;
        }

        public bool CanAfford(int amount) => amount <= coins;

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > coins) return false;
            coins -= amount;
            return true;
        }

        public void Restore(int savedCoins, int savedEarned)
        {
            coins = Math.Max(0, savedCoins);
            TotalEarned = Math.Max(0, savedEarned);
        }
    }
}
