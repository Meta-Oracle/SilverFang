using UnityEngine;

namespace SilverFang.Combat
{
    /// Damage attribute carried by every hit (melee or projectile). Drives the
    /// floating-number colour so players read what kind of damage landed.
    public enum DamageType
    {
        Kinetic,    // pure physical / standard rounds  -> white
        Nuclear,    // hardest-hitting radioactive payload -> toxic green
        Fire,       // incendiary / burning             -> ember orange
        Ice,        // cryo / freezing                  -> pale cyan
        Psychic,    // awakened-form energy             -> violet-blue
        Radiation,  // lingering fallout status         -> sick lime
        Void        // awakened void finishers          -> deep indigo
    }

    /// Central palette so Hitbox, Projectile, and the HUD all agree on what each
    /// damage attribute looks like. Criticals always override to gold regardless
    /// of element (a crit reads first, the element second).
    public static class DamageColors
    {
        public static readonly Color Crit = new Color(1f, 0.85f, 0.2f);   // yellow/gold
        public static readonly Color EnemyHit = new Color(1f, 0.35f, 0.3f); // damage to the player

        public static Color For(DamageType type)
        {
            switch (type)
            {
                case DamageType.Nuclear:   return new Color(0.55f, 1f, 0.2f);
                case DamageType.Fire:      return new Color(1f, 0.5f, 0.15f);
                case DamageType.Ice:       return new Color(0.55f, 0.85f, 1f);
                case DamageType.Psychic:   return new Color(0.6f, 0.55f, 1f);
                case DamageType.Radiation: return new Color(0.7f, 1f, 0.35f);
                case DamageType.Void:      return new Color(0.5f, 0.3f, 0.95f);
                default:                   return Color.white; // Kinetic
            }
        }

        /// Final colour for a landed hit: gold for crits, the player's hit tint
        /// for damage dealt to the player, otherwise the attribute colour.
        public static Color Resolve(DamageType type, bool crit, bool fromPlayer)
        {
            if (crit) return Crit;
            if (!fromPlayer) return EnemyHit;
            return For(type);
        }
    }
}
