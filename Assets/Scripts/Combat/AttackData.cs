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
                hitsBothSides = hitsBothSides
            };
        }
    }
}
