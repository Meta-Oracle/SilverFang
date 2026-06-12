namespace SilverFang.Progression
{
    /// Everything the progression system can enhance. Percent-based unless
    /// noted; MaxHealth and HealOnKill are flat values.
    public enum ModifierType
    {
        SwordDamage,
        GunDamage,
        AwakenedDamage,
        MoveSpeed,
        MaxHealth,        // flat HP
        Defense,          // % damage taken reduction
        TeleportDistance,
        AwakenedDuration,
        AwakenedGain,     // meter gain per hit
        StatusDuration,   // duration of statuses the player inflicts
        ChainWindow,      // combo input window
        ScematicaGain,
        XpGain,
        HealOnKill,       // flat HP restored per kill
        CritRate,         // % chance to land a critical hit
        CritDamage,       // % bonus damage on critical hits (on top of +50% base)
        ElementalResist   // % shorter statuses suffered by the player
    }
}
