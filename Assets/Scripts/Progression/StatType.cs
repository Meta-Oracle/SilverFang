namespace SilverFang.Progression
{
    /// Base character attributes. Each stat starts at PlayerProgression.BaseStat,
    /// rises automatically with level, and can be raised further with stat points.
    public enum StatType
    {
        Vitality,     // max health, damage resistance
        Strength,     // sword damage
        Marksmanship, // gun damage
        Agility,      // move speed, combo chain window
        Resonance,    // teleport distance, awakened duration/gain/damage
        Alchemy       // status effect duration, SCEM and XP gain
    }
}
