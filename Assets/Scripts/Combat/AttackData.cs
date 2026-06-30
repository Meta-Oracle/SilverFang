using UnityEngine;

namespace SilverFang.Combat
{
    [System.Serializable]
    public class AttackData
    {
        public int damage = 10;
        public float hitstun = 0.3f;
        public Vector2 knockback = new Vector2(3f, 0f);
        public bool knocksDown;
        /// Vertical launch velocity. > 0 sends the target airborne (juggle).
        public float launch;
        /// Hitbox shaping: multipliers applied to the attacker's base hitbox
        /// so thrusts reach far, cleaves swing tall, and spins cover both sides.
        public float rangeScale = 1f;
        public float heightScale = 1f;
        /// Extends the hitbox behind the attacker too (spins, whirlwinds).
        public bool hitsBothSides;
        /// Damage attribute -> drives the floating-number colour. Most melee is
        /// Kinetic (white); elemental moves and special rounds set their own.
        public DamageType damageType = DamageType.Kinetic;
        /// Heavy/charged impacts kick up a debris burst on contact.
        public bool spawnsDebris;
        // ---- Combat Fluidity Engine: bounce / extender mechanics ----
        /// Slams an airborne target into a wall: reverses horizontal momentum and
        /// pops them off it for a follow-up (needs a wall in the knockback path).
        public bool wallBounce;
        /// A juggled target that hits the ground bounces back up instead of
        /// landing into knockdown, keeping the juggle alive for an extender.
        public bool groundBounce;
        /// Off-the-ground: connects with a downed/knocked-down target and pops
        /// them airborne again (ground-pickup extender).
        public bool otg;
        /// Spike: drives an airborne target sharply DOWNWARD; the hard landing
        /// triggers a ground bounce so a spike sets up its own extender.
        public bool spike;
        /// Extra hitstun seconds added by the attacker's style (e.g. electric
        /// hits tack on ~3 frames). Stacked on top of the move's base hitstun.
        public float bonusHitstun;

        /// Returns a copy with extra hitstun folded in (clones so the shared move
        /// definition is never mutated).
        public AttackData WithBonusHitstun(float add)
        {
            if (add <= 0f) return this;
            var c = ScaledBy(1.0001f); // forces a fresh clone
            c.bonusHitstun += add;
            return c;
        }

        public AttackData ScaledBy(float damageMult)
        {
            if (Mathf.Approximately(damageMult, 1f)) return this;
            return new AttackData
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * damageMult)),
                hitstun = hitstun,
                knockback = knockback,
                knocksDown = knocksDown,
                launch = launch,
                rangeScale = rangeScale,
                heightScale = heightScale,
                hitsBothSides = hitsBothSides,
                damageType = damageType,
                spawnsDebris = spawnsDebris,
                wallBounce = wallBounce,
                groundBounce = groundBounce,
                otg = otg,
                spike = spike,
                bonusHitstun = bonusHitstun
            };
        }
    }
}
