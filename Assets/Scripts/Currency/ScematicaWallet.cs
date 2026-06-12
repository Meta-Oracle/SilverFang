using System;
using UnityEngine;

namespace SilverFang.Currency
{
    /// In-game Scematica ledger, persisted locally via PlayerPrefs.
    /// This is soft currency earned through gameplay — it does not move
    /// real on-chain tokens.
    public static class ScematicaWallet
    {
        private const string PrefsKey = "scematica_balance";
        private const long NotLoaded = long.MinValue;

        private static long balance = NotLoaded;

        public static long Balance
        {
            get
            {
                EnsureLoaded();
                return balance;
            }
        }

        public static event Action<long> OnBalanceChanged;

        public static void Earn(long amount)
        {
            if (amount <= 0) return;
            EnsureLoaded();
            balance += amount;
            Save();
            OnBalanceChanged?.Invoke(balance);
        }

        public static bool Spend(long amount)
        {
            EnsureLoaded();
            if (amount <= 0 || balance < amount) return false;
            balance -= amount;
            Save();
            OnBalanceChanged?.Invoke(balance);
            return true;
        }

        private static void EnsureLoaded()
        {
            if (balance != NotLoaded) return;
            long.TryParse(PlayerPrefs.GetString(PrefsKey, "0"), out balance);
        }

        private static void Save() => PlayerPrefs.SetString(PrefsKey, balance.ToString());
    }
}
