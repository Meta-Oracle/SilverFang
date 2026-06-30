using UnityEngine;

namespace SilverFang.Combat
{
    /// Projectile power tiers. Drives clash priority AND deflectability: a sword
    /// swing parries everything up to Heavy, but the top two classes (Beam, Apex)
    /// are too powerful to bat aside and punch through a swing.
    public enum PowerClass { Light, Medium, Heavy, Beam, Apex }

    /// The TRUE-AIM ENGINE — Silver Fang's dedicated projectile ballistics. One
    /// rule set for every round, bullet, and energy wave: barrel-tip origin,
    /// power classification, how much force a projectile carries, how opposing
    /// projectiles CLASH (the stronger punches through, equals annihilate), and
    /// what a defender can and cannot deflect. Runs alongside the Debris Engine
    /// (both are projectile/shrapnel systems) under the Fang Engine.
    public static class CombatBallistics
    {
        /// Auto-derive a round's clash strength when the ammo doesn't set one:
        /// scaled damage, with bonuses for piercing rounds and persistent beams.
        public static float AutoStrength(AmmoDefinition ammo, float damageScale)
        {
            if (ammo == null) return 1f;
            if (ammo.strength > 0f) return ammo.strength;
            float s = Mathf.Max(1f, (ammo.attack != null ? ammo.attack.damage : 1) * Mathf.Max(0.5f, damageScale));
            if (ammo.piercing) s += 8f;
            if (ammo.isBeam) s += 30f;   // beams are heavy/persistent; they shove through
            return s;
        }

        /// Classify a round's power tier from its resolved clash strength.
        public static PowerClass ClassOf(float strength)
        {
            if (strength >= 50f) return PowerClass.Apex;
            if (strength >= 30f) return PowerClass.Beam;
            if (strength >= 16f) return PowerClass.Heavy;
            if (strength >= 8f) return PowerClass.Medium;
            return PowerClass.Light;
        }

        /// A sword swing / active-attack frame deflects everything UP TO Heavy.
        /// The two highest classes (Beam, Apex) cannot be batted aside.
        public static bool SwordCanDeflect(PowerClass pc) => pc < PowerClass.Beam;

        public enum ClashResult { Win, Lose, Mutual }

        /// Resolve a clash between two opposing rounds of the given strengths.
        /// Returns the result for the FIRST round; out leftover is its remaining
        /// strength if it punched through (Win).
        public static ClashResult Resolve(float a, float b, out float leftover)
        {
            const float eps = 0.5f;
            if (a > b + eps) { leftover = a - b; return ClashResult.Win; }
            if (b > a + eps) { leftover = 0f; return ClashResult.Lose; }
            leftover = 0f; return ClashResult.Mutual;
        }
    }
}
