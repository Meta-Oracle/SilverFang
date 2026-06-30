using UnityEngine;

namespace SilverFang.Combat
{
    public enum AmmoType
    {
        Standard,
        Nuclear,
        Ice,
        Incendiary,
        Piercing
    }

    [System.Serializable]
    public class AmmoDefinition
    {
        public AmmoType type = AmmoType.Standard;
        public AttackData attack = new AttackData();
        public Color tint = Color.white;
        public float speed = 14f;
        public bool piercing;
        public StatusType status = StatusType.None;
        public float statusDuration;
        /// Phantom-slash style: launches fast then decelerates and fades out.
        public bool decelerates;
        /// Beam round: pierces everything and re-hits each enemy on an interval
        /// (multihit) as it passes through, instead of a single one-shot hit.
        public bool isBeam;
        /// Seconds between successive beam ticks on the same target.
        public float beamTickInterval;
        /// Sticky beam: each tick pins the caught enemy in place (held in the
        /// beam path) on top of the damage/hitstun.
        public bool sticky;
        /// Ballistics: force carried into a projectile CLASH. 0 = auto-derive from
        /// damage (+ beam/pierce bonus). A stronger round punches through weaker
        /// ones; equal rounds annihilate each other.
        public float strength;
        /// Overrides the attribute inferred from `type`. Left Kinetic, the
        /// attribute is derived from the ammo type; awakened rounds set Psychic.
        public DamageType damageTypeOverride = DamageType.Kinetic;

        /// The damage attribute this round paints on impact. Explicit override
        /// wins (awakened psychic); otherwise it follows the ammo type.
        public DamageType ResolveDamageType()
        {
            if (damageTypeOverride != DamageType.Kinetic) return damageTypeOverride;
            switch (type)
            {
                case AmmoType.Nuclear:     return DamageType.Nuclear;
                case AmmoType.Ice:         return DamageType.Ice;
                case AmmoType.Incendiary:  return DamageType.Fire;
                default:                   return DamageType.Kinetic; // Standard / Piercing
            }
        }

        /// Awakened overcharge: the round turns blue and gains psychic damage
        /// while KEEPING its established element (status + base attack intact).
        public AmmoDefinition AsAwakened()
        {
            return new AmmoDefinition
            {
                type = type,
                attack = attack,
                tint = new Color(0.55f, 0.7f, 1f),   // all awakened shots read blue
                speed = speed,
                piercing = piercing,
                status = status,                     // established element still applies
                statusDuration = statusDuration,
                decelerates = decelerates,
                damageTypeOverride = DamageType.Psychic
            };
        }
    }
}
