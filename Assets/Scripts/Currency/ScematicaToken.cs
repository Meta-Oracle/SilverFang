namespace SilverFang.Currency
{
    /// Scematica (SCEM) — the SPL token on Solana that the in-game currency
    /// is branded after. On-chain interaction is read-only (see
    /// ScematicaChainBalance); the game never holds keys or signs transactions.
    public static class ScematicaToken
    {
        public const string Name = "Scematica";
        public const string Symbol = "SCEMA";
        public const string Chain = "Solana";
        public const string MintAddress = "AbKiP2Jc6nM7937jTDfqoJC1bsg5FQ24Buk2iqRFump";
    }
}
